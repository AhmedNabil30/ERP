using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// D-225 item 2 (site photos kept on the server's disk) and item 1 (the owner reads the daily log). Files go to a temp folder
/// the fixture deletes.
/// </summary>
public sealed partial class DailyLogTests
{
    // Minimal images that parse to the end: the strippers refuse a file that does not (no unread tail is ever copied).
    private static readonly byte[] Jpeg = JpegImage();

    private static readonly byte[] Png = PngImage();

    private static readonly byte[] Webp = WebpImage();

    /// <summary>SOI, JFIF, a table, a frame, a scan and EOI, with <paramref name="beforeScan"/> segments after the JFIF and <paramref name="afterEoi"/> behind EOI.</summary>
    private static byte[] JpegImage(byte[]? beforeScan = null, byte[]? afterEoi = null, bool fillBytes = false)
    {
        byte[] fill = fillBytes ? [0xFF, 0xFF] : [];
        return
        [
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 1, 0, 0, 1, 0, 1, 0, 0,
            .. (beforeScan ?? []),
            .. fill,
            0xFF, 0xDB, 0x00, 0x43, 0x00, .. new byte[64],
            0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
            0xFF, 0xC4, 0x00, 0x14, 0x00, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            .. fill,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
            0x2A, 0xFF, 0x00, 0x11,
            0xFF, 0xD9,
            .. (afterEoi ?? []),
        ];
    }

    private static byte[] Segment(byte marker, byte[] payload) =>
        [0xFF, marker, (byte)((payload.Length + 2) >> 8), (byte)((payload.Length + 2) & 0xFF), .. payload];

    private static byte[] PngChunk(string type, byte[] data) =>
        [0, 0, (byte)(data.Length >> 8), (byte)(data.Length & 0xFF), .. System.Text.Encoding.ASCII.GetBytes(type), .. data, 0, 0, 0, 0];

    private static byte[] PngImage(byte[]? extraChunk = null) =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        .. PngChunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 0, 0, 0, 0]),
        .. (extraChunk ?? []),
        .. PngChunk("IDAT", [1, 2, 3, 4]),
        .. PngChunk("IEND", []),
    ];

    private static byte[] WebpChunk(string type, byte[] data) =>
        [.. System.Text.Encoding.ASCII.GetBytes(type), (byte)data.Length, 0, 0, 0, .. data, .. (data.Length % 2 == 1 ? new byte[] { 0 } : [])];

    private static byte[] WebpImage(bool withExif = false)
    {
        byte[] body =
        [
            .. "WEBP"u8,
            .. WebpChunk("VP8X", [withExif ? (byte)0x08 : (byte)0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
            .. WebpChunk("VP8 ", [1, 2, 3, 4]),
            .. (withExif ? WebpChunk("EXIF", [.. "GPSLatitude"u8]) : []),
        ];
        return [.. "RIFF"u8, (byte)body.Length, 0, 0, 0, .. body];
    }

    private static string Photos(Guid project) => $"{Logs(project)}/photos";

    private Task<HttpResponseMessage> UploadAsync(Guid project, byte[] bytes, Guid actor, string contentType = "image/jpeg")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return SendAsync(HttpMethod.Post, $"{Photos(project)}/upload", actor, null, content: content);
    }

    private async Task<string> UploadedAsync(Guid project, byte[]? bytes = null, Guid? actor = null)
    {
        HttpResponseMessage response = await UploadAsync(project, bytes ?? Jpeg, actor ?? _engineerA);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("reference").GetString()!;
    }

    private Task<HttpResponseMessage> RecordPhoto(Guid project, string reference, Guid? entryId = null, Guid? actor = null) =>
        SendAsync(
            HttpMethod.Post, Photos(project), actor ?? _engineerA,
            new { entryId = entryId ?? Guid.NewGuid(), fileReference = reference, caption = "slab", occurredAt = At(9, 0) });

    private async Task<(Guid Project, string Reference)> RecordedPhotoAsync(byte[]? bytes = null)
    {
        (Guid project, _) = await NewActiveProjectAsync();
        string reference = await UploadedAsync(project, bytes);
        (await RecordPhoto(project, reference)).StatusCode.Should().Be(HttpStatusCode.Created);
        return (project, reference);
    }

    // ---- photos: the happy path -----------------------------------------------------------------------------------

    [Fact]
    public async Task Photo_uploaded_then_recorded_then_downloaded_returns_the_same_bytes_with_the_sniffed_type()
    {
        (Guid project, string reference) = await RecordedPhotoAsync(Png);

        HttpResponseMessage download = await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _engineerA, null);

        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(Png);
        download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        download.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Theory]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public async Task Jpeg_and_webp_are_accepted_and_served_with_their_own_type(string kind, string expected)
    {
        (Guid project, string reference) = await RecordedPhotoAsync(kind == "jpeg" ? Jpeg : Webp);

        HttpResponseMessage download = await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _engineerA, null);

        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be(expected);
    }

    [Fact]
    public async Task An_uploaded_photo_is_stored_and_served_without_its_metadata()
    {
        byte[] secondImage = JpegImage([.. Segment(0xE1, [.. "Exif\0\0GPSSecond"u8])]);
        (byte[] Original, string Marker)[] cases =
        [
            (JpegImage([.. Segment(0xE1, [.. "Exif\0\0GPSLatitude"u8]), .. Segment(0xFE, [.. "COMMENTAUTHOR"u8])]), "GPSLatitude"),
            (JpegImage(afterEoi: secondImage), "GPSSecond"),
            (JpegImage(fillBytes: true, beforeScan: Segment(0xE1, [.. "Exif\0\0GPSFill"u8])), "GPSFill"),
            (PngImage(PngChunk("tEXt", [.. "GPSPng\0x"u8])), "GPSPng"),
            (WebpImage(withExif: true), "GPSLatitude"),
        ];

        foreach ((byte[] original, string marker) in cases)
        {
            (Guid project, string reference) = await RecordedPhotoAsync(original);
            HttpResponseMessage download = await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _engineerA, null);
            byte[] served = await download.Content.ReadAsByteArrayAsync(Ct);

            System.Text.Encoding.Latin1.GetString(served).Should().NotContain(marker).And.NotContain("COMMENTAUTHOR");
            served.Length.Should().BeLessThan(original.Length);
            Directory.GetFiles(_photoRoot, "*", SearchOption.AllDirectories)
                .Select(file => System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(file)))
                .Should().NotContain(text => text.Contains(marker, StringComparison.Ordinal), "the stored bytes carry none either");
        }
    }

    [Fact]
    public async Task A_photo_that_does_not_read_cleanly_to_its_end_is_refused_and_nothing_is_stored()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        byte[] truncated = Jpeg[..^6];
        byte[] noEnd = PngImage()[..^12];

        foreach (byte[] bad in new[] { truncated, noEnd })
        {
            HttpResponseMessage response = await UploadAsync(project, bad, _engineerA);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.daily_log.photo_unreadable");
        }

        (Directory.Exists(_photoRoot) ? Directory.GetFiles(_photoRoot, "*", SearchOption.AllDirectories) : []).Should().BeEmpty("a refused photo leaves nothing on disk");
    }

    [Fact]
    public async Task The_stored_file_is_named_by_the_server_and_the_clients_name_and_type_do_not_matter()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        var content = new ByteArrayContent(Png);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "../../evil.php" };

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"{Photos(project)}/upload", _engineerA, null, content: content);

        response.StatusCode.Should().Be(HttpStatusCode.Created, "the bytes are a PNG whatever the client called them");
        string[] files = Directory.GetFiles(_photoRoot, "*", SearchOption.AllDirectories);
        files.Should().ContainSingle();
        Path.GetFileName(files[0]).Should().NotContain("evil");
        Path.GetFullPath(files[0]).Should().StartWith(Path.GetFullPath(_photoRoot));
    }

    [Fact]
    public async Task A_retried_entry_id_does_not_double_the_photo()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        string reference = await UploadedAsync(project);
        Guid entryId = Guid.NewGuid();

        (await RecordPhoto(project, reference, entryId)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await RecordPhoto(project, reference, entryId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await CountAsync("photo_entries", project)).Should().Be(1);
    }

    [Fact]
    public async Task The_log_reads_give_each_photo_the_route_that_fetches_it_and_no_disk_path()
    {
        (Guid project, string reference) = await RecordedPhotoAsync();

        JsonElement mine = await MineAsync(project);
        JsonElement photo = mine.GetProperty("photos")[0];

        photo.GetProperty("url").GetString().Should().Be($"/api/projects/{project}/daily-logs/photos/{reference}");
        mine.GetRawText().Should().NotContain(_photoRoot.Replace("\\", "\\\\", StringComparison.Ordinal));

        JsonElement review = await ReviewAsync(project, "2026-09-25", _owner, _engineerA);
        review.GetProperty("photos")[0].GetProperty("url").GetString().Should().Be(photo.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Recording_a_photo_still_obeys_the_check_in_gate_and_the_window()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        string reference = await UploadedAsync(project);

        HttpResponseMessage noCheckIn = await SendAsync(
            HttpMethod.Post, Photos(project), _engineerB,
            new { entryId = Guid.NewGuid(), fileReference = reference, occurredAt = At(9, 0) }, autoCheckIn: false);
        HttpResponseMessage tooOld = await SendAsync(
            HttpMethod.Post, Photos(project), _engineerA,
            new { entryId = Guid.NewGuid(), fileReference = reference, occurredAt = Dated(5) });

        noCheckIn.StatusCode.Should().Be(HttpStatusCode.Conflict);
        tooOld.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountAsync("photo_entries", project)).Should().Be(0);
    }

    // ---- photos: the trust boundary -------------------------------------------------------------------------------

    [Fact]
    public async Task Upload_is_refused_to_the_owner_finance_and_an_engineer_who_is_not_assigned()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        foreach (Guid refused in new[] { _owner, _finance, _engineerUnassigned })
        {
            (await UploadAsync(project, Jpeg, refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{refused}");
        }

        Directory.Exists(_photoRoot).Should().BeTrue("the folder is created on start");
        Directory.GetFiles(_photoRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_over_the_size_cap_is_refused_and_nothing_is_stored()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        byte[] big = new byte[(10 * 1024 * 1024) + 1];
        Jpeg.CopyTo(big, 0);

        HttpResponseMessage response = await UploadAsync(project, big, _engineerA);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        Directory.GetFiles(_photoRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("empty")]
    [InlineData("html-named-jpeg")]
    public async Task Bytes_that_are_not_an_image_are_refused_whatever_the_client_calls_them(string kind)
    {
        (Guid project, _) = await NewActiveProjectAsync();
        byte[] bytes = kind switch
        {
            "empty" => [],
            "html-named-jpeg" => "<html><script>alert(1)</script></html>"u8.ToArray(),
            _ => "not a picture"u8.ToArray(),
        };

        HttpResponseMessage response = await UploadAsync(project, bytes, _engineerA, "image/jpeg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.photo_not_an_image");
        Directory.GetFiles(_photoRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\win.ini")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("/etc/passwd")]
    [InlineData("photos/a.jpg")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-reference")]
    public async Task Recording_a_photo_refuses_a_reference_that_is_not_one_this_project_holds(string reference)
    {
        (Guid project, _) = await NewActiveProjectAsync();

        HttpResponseMessage response = await RecordPhoto(project, reference);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.file_reference_invalid");
        (await CountAsync("photo_entries", project)).Should().Be(0);
    }

    [Fact]
    public async Task Recording_a_photo_refuses_a_reference_uploaded_to_another_project()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        (Guid other, _) = await NewActiveProjectAsync();
        string foreign = await UploadedAsync(other);

        HttpResponseMessage response = await RecordPhoto(project, foreign);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.file_reference_invalid");
        (await CountAsync("photo_entries", project)).Should().Be(0);
    }

    [Fact]
    public async Task Download_is_refused_to_an_engineer_of_another_project_and_to_roles_outside_the_log()
    {
        (Guid project, string reference) = await RecordedPhotoAsync();
        (Guid other, _) = await NewActiveProjectAsync();

        // The photo's own reference asked for through a project the caller is assigned to: that project holds no such file.
        (await SendAsync(HttpMethod.Get, $"{Photos(other)}/{reference}", _engineerA, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        foreach (Guid refused in new[] { _engineerUnassigned, _finance })
        {
            (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, $"{refused}");
        }
    }

    [Fact]
    public async Task Download_follows_the_log_read_rules_a_junior_sees_his_own_and_the_owner_and_supervisor_see_all()
    {
        (Guid project, string reference) = await RecordedPhotoAsync();

        (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _engineerA, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _supervisor, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _engineerB, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "a junior reads his own photos, and another engineer's are not his to see");
    }

    [Fact]
    public async Task An_uploaded_file_no_entry_refers_to_cannot_be_downloaded()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        string reference = await UploadedAsync(project);

        (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", _owner, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    // ---- the owner reads the daily log (D-225 item 1) -------------------------------------------------------------

    [Fact]
    public async Task The_owner_reads_every_engineers_log_of_the_day_and_may_correct_crew_but_not_quantities()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid quantity = await SavedQuantityAsync(project, line, 5m, At(9, 0), _engineerA);
        Guid crew = await SavedCrewAsync(project, 8m, At(9, 0), _engineerB);
        await SavedQuantityAsync(project, line, 2m, At(10, 0), _engineerB);

        JsonElement list = await ReadAsync(await SendAsync(HttpMethod.Get, $"{Logs(project)}?date=2026-09-25", _owner, null));

        list.EnumerateArray().Select(item => item.GetProperty("engineerId").GetGuid())
            .Should().BeEquivalentTo([_engineerA, _engineerB]);
        JsonElement a = list.EnumerateArray().Single(item => item.GetProperty("engineerId").GetGuid() == _engineerA).GetProperty("log");
        a.GetProperty("quantities")[0].GetProperty("canCorrect").GetBoolean().Should().BeFalse("D-219: the owner never corrects a quantity");
        JsonElement b = list.EnumerateArray().Single(item => item.GetProperty("engineerId").GetGuid() == _engineerB).GetProperty("log");
        b.GetProperty("dayLabour")[0].GetProperty("canCorrect").GetBoolean().Should().BeTrue("KAFF-615 AC-5: the owner corrects crew");

        (await CorrectQty(project, quantity, 3m, _owner)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CorrectCrew(project, crew, 6m, _owner)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task The_owner_opens_no_write_route_of_the_daily_log()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        string reference = await UploadedAsync(project);

        foreach ((HttpMethod method, string route, object? body) in AllCalls(project, line))
        {
            (await SendAsync(method, route, _owner, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
        }

        (await UploadAsync(project, Jpeg, _owner)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RecordPhoto(project, reference, actor: _owner)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Role.TechnicalOffice)]
    [InlineData(Role.MarketingSales)]
    [InlineData(Role.Finance)]
    public async Task Technical_office_marketing_and_finance_read_no_daily_log_and_no_photo(Role role)
    {
        (Guid project, string reference) = await RecordedPhotoAsync();
        Guid actor = await NewUserAsync(role);

        (await SendAsync(HttpMethod.Get, $"{Logs(project)}?date=2026-09-25", actor, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date=2026-09-25", actor, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"{Photos(project)}/{reference}", actor, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> NewUserAsync(Role role)
    {
        await using KaffDbContext context = _database.CreateContext();
        User user = role == Role.Finance
            ? MakeUser("dl-extra-fin", role, WellKnownDepartments.FinanceId)
            : MakeUser("dl-extra", role);
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }
}
