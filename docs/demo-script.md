# Kaff ERP — client demo script

One page. About 15 minutes. Drive it yourself; do not let the client click first.

**URL:** http://localhost:4200 (the Angular app; it talks to the API on 5080). On staging, use the HTTPS name, never a bare IP, because the sign-in cookie is `Secure`.
**Owner login:** `owner_demo` / `Demo#Owner1`
**Other logins (made by `scripts/seed-demo.ps1`; they must change their password on first sign-in, so use them only if you have already done that):** `sara_finance_demo` / `Demo#Fin123` (Finance), `hend_hr_demo` / `Demo#Hr123` (HR), `karim_sales_demo` / `Demo#Sales123` (Marketing and Sales).
**Before the client arrives:** sign in once as the Owner on the machine you will present from. Confirm the catalogue has a few items and the أبواب tree is populated. Have one small `.xlsx` ready (download the template from the import page, fill 3 rows) and a second copy with one deliberately wrong row. Open the app in light mode, browser at 100%, phone-width window ready as a second tab.

## Walkthrough

1. **Sign-in screen.** Say: "This is Kaff's own look. The deep green and cream come from your logo, and the whole system is Arabic first, right to left." Sign in as `owner_demo`.
2. **The shell.** Point at the side navigation. Say: "Each person sees only what their role allows. The menu is a convenience; the server refuses the rest even if someone types the address." Optional: sign out and sign in as HR to show a shorter menu.
3. **Catalogue list.** Open the catalogue. Say: "Every material, labour item and subcontract item Kaff prices, grouped under its باب." Show the groups, search and filter.
4. **Create panel.** Add one item. Say: "A panel, not a new page; you keep your place in the list." Save and show it appear under the right باب.
5. **Excel import (linger here).** Open Import. Say: "Kaff already has this in Excel. Nothing changes until you confirm." Download the template to show the required columns. Upload the good file: show the preview (new, changed, unchanged), then confirm and show the result. Then upload the file with the bad row: show that the row is named, with the reason, and that nothing was written.
6. **Module أبواب (linger here).** Open the tree. Say: "This is how Kaff's work is organised, and the markup lives here." Open the markup editor on one باب and change the percentage. Move a باب under another with the move dialog. Archive an empty باب, then restore it. Say: "A باب that still has active items cannot be archived; the system says why in Arabic."
7. **Older screens, quick pass (about 2 minutes).** Clients (try a duplicate phone number: a live warning appears), employees, users, subcontractors, suppliers, departments. Say: "Same design, same behaviour."
8. **Audit trail (linger here).** Open it as Owner. Say: "Everything you just did is already here: who, when, what it was before and after. Nobody can edit it." Find the import or the markup change you made.
9. **Look and feel.** Switch to dark mode, then show the phone-width tab. Say: "Site staff will use the phone. Same screens, same Arabic, same rules."

## The moments worth lingering on

- **The Excel import:** preview before commit, and a bad file changes nothing.
- **The أبواب module:** markup, move, archive and restore, with the refusal when items are still active.
- **The audit trail:** trust. Everything above is already recorded.

## If something goes wrong

Do not debug in front of the client. Refresh once. If the app shows empty data or errors, say "I will show you that part on the recorded screens", skip to the next step, and fix it afterwards. If sign-in fails, wait: five wrong attempts lock the account for 15 minutes, so stop retrying.

## What is not built yet

Say this plainly, before they ask: projects exist only as a list. There is no BOQ, no extracts, no treasury or payment screens, no daily log, and no client portal. The catalogue, أبواب and the people and company records are the foundation those will sit on. Do not promise dates; give them the next meeting to see the first of them.
