// Rich demo data through the real API. Re-runnable: catalogue codes are unique (409 = skip);
// clients, suppliers, subcontractors and employees are skipped when their phone already appears in the list.
// Usage: node scripts/seed-demo-rich.mjs [baseUrl]   (owner_demo / Demo#Owner1 must exist)
const base = process.argv[2] ?? 'http://localhost:5080';
let cookie = '';
async function call(method, path, body) {
  const r = await fetch(base + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(cookie ? { Cookie: cookie } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const sc = r.headers.get('set-cookie');
  if (sc) cookie = sc.split(';')[0];
  const text = await r.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* empty body */ }
  return { status: r.status, json, text };
}
const si = await call('POST', '/api/auth/sign-in', { userName: 'owner_demo', password: 'Demo#Owner1' });
if (si.status !== 204) throw new Error('sign-in failed ' + si.status + si.text);

const babs = (await call('GET', '/api/babs')).json.items;
const bab = (c) => babs.find((b) => b.code === c)?.id;

const M3 = 'م٣', M2 = 'م٢', ML = 'متر طولي', EA = 'عدد', TON = 'طن', LS = 'مقطوعية';
// [code prefix, bab code, markup, items [descAr, descEn, unit, cost]]
const catalogue = [
  ['CON', 'CON', 0.15, [
    ['خرسانة عادية للقواعد', 'Plain concrete for footings', M3, 1450],
    ['خرسانة مسلحة للقواعد المنفصلة', 'Reinforced concrete, isolated footings', M3, 1900],
    ['خرسانة مسلحة للأعمدة', 'Reinforced concrete, columns', M3, 2050],
    ['خرسانة مسلحة للكمرات', 'Reinforced concrete, beams', M3, 2000],
    ['خرسانة مسلحة للأسقف', 'Reinforced concrete, slabs', M3, 1950],
    ['خرسانة مسلحة للسلالم', 'Reinforced concrete, stairs', M3, 2150],
    ['حديد تسليح عالي المقاومة', 'High-tensile reinforcement steel', TON, 36500],
    ['نجارة مسلحة (شدات خشبية)', 'Timber formwork', M2, 210],
    ['شدة معدنية للأعمدة', 'Metal formwork, columns', M2, 260],
    ['عزل رطوبة للقواعد بالبيتومين', 'Bitumen waterproofing for footings', M2, 95],
    ['ردم بالرمل المدموك', 'Compacted sand backfill', M3, 180],
    ['حفر عام للأساسات', 'General excavation for foundations', M3, 85],
    ['أرصفة خرسانية سابقة الصب', 'Precast concrete kerbs', ML, 120],
  ]],
  ['MAS', 'MAS', 0.15, [
    ['مباني طوب أحمر 12 سم', 'Red brick masonry 12 cm', M2, 245],
    ['مباني طوب أحمر 25 سم', 'Red brick masonry 25 cm', M2, 430],
    ['مباني طوب طفلي 20 سم', 'Hollow clay block masonry 20 cm', M2, 360],
    ['مباني طوب أسمنتي مفرغ 20 سم', 'Hollow cement block 20 cm', M2, 310],
    ['مباني طوب أسمنتي مصمت', 'Solid cement block', M2, 340],
    ['لياسة أسمنتية للحوائط الداخلية', 'Internal cement plaster', M2, 130],
    ['لياسة أسمنتية للواجهات', 'External cement plaster', M2, 165],
    ['بياض محارة للأسقف', 'Ceiling plaster', M2, 150],
    ['أعتاب خرسانية جاهزة', 'Precast lintels', ML, 190],
    ['عزل حراري للحوائط الخارجية', 'Thermal insulation, external walls', M2, 220],
    ['تكسيرات وفتحات في المباني', 'Chasing and openings', LS, 4500],
    ['تركيب حلوق الأبواب', 'Door frames setting', EA, 320],
  ]],
  ['FIN', 'FIN', 0.3, [
    ['بلاط سيراميك أرضيات 60×60', 'Ceramic floor tiles 60x60', M2, 520],
    ['بلاط بورسلين أرضيات 80×80', 'Porcelain floor tiles 80x80', M2, 780],
    ['رخام جلالة للأرضيات', 'Galala marble flooring', M2, 1450],
    ['جرانيت أسواني للسلالم', 'Aswan granite for stairs', ML, 980],
    ['سيراميك حوائط حمامات', 'Bathroom wall ceramic', M2, 480],
    ['دهان بلاستيك وجهين', 'Plastic paint, two coats', M2, 48],
    ['دهان بلاستيك فاخر مع معجون', 'Premium paint with putty', M2, 85],
    ['دهان زيت للأبواب والشبابيك', 'Oil paint for doors and windows', M2, 110],
    ['أسقف جبس بورد مستوية', 'Flat gypsum board ceiling', M2, 330],
    ['أسقف جبس بورد مع كرانيش', 'Gypsum ceiling with cornice', M2, 460],
    ['وزرة رخام ارتفاع 10 سم', 'Marble skirting 10 cm', ML, 120],
    ['باركيه خشب زان', 'Beech parquet flooring', M2, 950],
    ['ورق حائط مستورد', 'Imported wallpaper', M2, 420],
    ['حجر هاشمي للواجهات', 'Hashemi stone facade', M2, 700],
    ['رخام للمطابخ (سطح)', 'Marble kitchen countertop', ML, 1600],
  ]],
  ['ELE', 'ELE', 0.2, [
    ['نقطة إنارة سقف', 'Ceiling lighting point', EA, 340],
    ['نقطة بريزة عادية', 'Standard socket point', EA, 390],
    ['نقطة بريزة تكييف', 'AC socket point', EA, 560],
    ['نقطة تليفزيون وانترنت', 'TV and data point', EA, 310],
    ['لوحة توزيع رئيسية 24 خط', 'Main distribution board 24-way', EA, 7800],
    ['لوحة توزيع فرعية 12 خط', 'Sub distribution board 12-way', EA, 3600],
    ['كابل نحاس 3×6 مم', 'Copper cable 3x6 mm', ML, 135],
    ['كابل نحاس 3×16 مم', 'Copper cable 3x16 mm', ML, 330],
    ['مواسير بي في سي مخفية', 'Concealed PVC conduit', ML, 28],
    ['نظام تأريض كامل', 'Complete earthing system', LS, 9500],
    ['كشاف LED غاطس', 'Recessed LED downlight', EA, 260],
    ['جرس باب مع انتركم', 'Door bell with intercom', EA, 1250],
    ['مفتاح إنارة مفرد', 'Single light switch', EA, 90],
  ]],
  ['PLU', 'PLU', 0.2, [
    ['مواسير تغذية PPR قطر 25 مم', 'PPR supply pipe 25 mm', ML, 62],
    ['مواسير تغذية PPR قطر 32 مم', 'PPR supply pipe 32 mm', ML, 88],
    ['مواسير صرف بي في سي 4 بوصة', 'PVC drain pipe 4 inch', ML, 105],
    ['مواسير صرف بي في سي 2 بوصة', 'PVC drain pipe 2 inch', ML, 55],
    ['نقطة مياه ساخنة وباردة', 'Hot and cold water point', EA, 620],
    ['نقطة صرف أرضي', 'Floor drain point', EA, 410],
    ['خزان مياه علوي 2000 لتر', 'Overhead water tank 2000 L', EA, 6200],
    ['طلمبة رفع مياه 1 حصان', 'Booster pump 1 HP', EA, 5400],
    ['حوض مطبخ ستانلس مع خلاط', 'Stainless sink with mixer', EA, 3100],
    ['طقم حمام كامل (مرحاض وحوض)', 'Complete bathroom set', EA, 8900],
    ['سخان كهرباء 80 لتر', 'Electric water heater 80 L', EA, 4800],
    ['غرفة تفتيش خرسانية', 'Concrete manhole', EA, 5200],
  ]],
  ['CAR', 'CAR', 0.2, [
    ['باب خشب زان داخلي', 'Interior beech door', EA, 5400],
    ['باب خشب مصفح مدخل شقة', 'Apartment entrance armoured door', EA, 9800],
    ['شباك خشب مع شيش', 'Timber window with shutter', M2, 2400],
    ['مطبخ خشب mdf', 'MDF kitchen cabinets', ML, 5200],
    ['دولاب حائط غرفة نوم', 'Built-in wardrobe', ML, 4600],
    ['درابزين سلم خشب', 'Timber stair handrail', ML, 1300],
    ['تجليد حوائط بالخشب', 'Timber wall cladding', M2, 1100],
    ['ضلف أبواب داخلية ماسيف', 'Solid interior door leaf', EA, 3900],
    ['كرانيش خشب للأسقف', 'Timber cornice', ML, 160],
    ['إكسسوارات ومفصلات', 'Hardware and hinges', LS, 1800],
  ]],
  ['MET', 'MET', 0.2, [
    ['شباك ألوميتال سحاب', 'Aluminium sliding window', M2, 2300],
    ['باب ألوميتال بلكونة', 'Aluminium balcony door', M2, 2600],
    ['واجهة زجاج ستركتشرال', 'Structural glazing facade', M2, 4200],
    ['درابزين حديد مشغول', 'Wrought iron balustrade', ML, 1900],
    ['درابزين ستانلس ستيل', 'Stainless steel balustrade', ML, 2700],
    ['سلم حديد هيكلي', 'Steel structural stair', TON, 52000],
    ['باب حديد جراج', 'Steel garage door', M2, 1850],
    ['بوابة حديد خارجية', 'Steel external gate', M2, 2100],
    ['مظلة حديد وبولي كربونات', 'Steel and polycarbonate canopy', M2, 1450],
    ['شبك حماية شبابيك', 'Window security grill', M2, 780],
  ]],
  ['HVA', 'HVA', 0.2, [
    ['تكييف سبليت 1.5 حصان', 'Split AC 1.5 HP', EA, 17500],
    ['تكييف سبليت 3 حصان', 'Split AC 3 HP', EA, 31000],
    ['مواسير نحاس تكييف', 'AC copper pipework', ML, 210],
    ['شفاط حمام', 'Bathroom extractor fan', EA, 850],
    ['دكت تهوية مجلفن', 'Galvanised ventilation duct', M2, 520],
    ['وحدة تكييف مركزي', 'Central AC unit', EA, 95000],
  ]],
];

let created = 0, skipped = 0, archived = 0, failed = 0;
for (const [prefix, babCode, markup, items] of catalogue) {
  const babId = bab(babCode);
  if (!babId) { console.log('bab missing', babCode); continue; }
  let n = 1;
  for (const [ar, en, unit, cost] of items) {
    const code = `${prefix}-${3000 + n}`;
    const r = await call('POST', '/api/catalogue-items', {
      code, descriptionAr: ar, descriptionEn: en, unit, babId,
      costPrice: cost, baseSellRate: Math.round(cost * (1 + markup)),
    });
    if (r.status === 201 || r.status === 200) {
      created++;
      // archive the last item of each bab so the مؤرشف filter has rows
      if (n === items.length && r.json?.id) {
        const a = await call('POST', `/api/catalogue-items/${r.json.id}/archive`);
        if (a.status < 300) archived++;
      }
    } else if (r.status === 409) skipped++;
    else { failed++; console.log('FAIL', code, r.status, r.text.slice(0, 200)); }
    n++;
  }
}

async function ensure(path, records) {
  const text = JSON.stringify((await call('GET', path)).json ?? '');
  let c = 0, s = 0;
  for (const rec of records) {
    if (text.includes(rec.phone)) { s++; continue; }
    const r = await call('POST', path, rec);
    if (r.status < 300) c++; else console.log('FAIL', path, r.status, r.text.slice(0, 200));
  }
  return `${c} created, ${s} existed`;
}

console.log('clients:', await ensure('/api/clients', [
  ['شركة النخبة للاستثمار العقاري', '01011112201', 'Corporate', 'التجمع الأول، القاهرة'],
  ['مجموعة الأمل للتطوير', '01011112202', 'Corporate', 'الشيخ زايد، الجيزة'],
  ['م. أحمد عبد الرحمن', '01011112203', 'Individual', 'مدينة نصر، القاهرة'],
  ['د. منى السيد', '01011112204', 'Individual', 'المعادي، القاهرة'],
  ['أ. محمود فؤاد', '01011112205', 'Individual', 'الدقي، الجيزة'],
  ['شركة السلام للفنادق', '01011112206', 'Corporate', 'شرم الشيخ، جنوب سيناء'],
].map(([name, phone, kind, address]) => ({ name, phone, kind, address, acknowledgedDuplicatePhone: false }))));

console.log('suppliers:', await ensure('/api/suppliers', [
  ['شركة الحديد والصلب المصرية', '01022223301', 'العاشر من رمضان'],
  ['مصنع السويس للأسمنت', '01022223302', 'السويس'],
  ['الشرقية للسيراميك والبورسلين', '01022223303', 'العبور'],
  ['النور للمواد الكهربائية', '01022223304', 'العتبة، القاهرة'],
  ['دلتا للأدوات الصحية', '01022223305', 'شبرا، القاهرة'],
].map(([name, phone, address]) => ({ name, phone, address, acknowledgedDuplicatePhone: false }))));

console.log('subcontractors:', await ensure('/api/subcontractors', [
  ['الأسطى حسن للمباني', '01033334401', 'MAS', 0.05],
  ['شركة المهندسين للكهرباء', '01033334402', 'ELE', 0.05],
  ['ورشة الفتح للألوميتال', '01033334403', 'MET', 0.1],
  ['الصفوة للسباكة والصرف', '01033334404', 'PLU', 0.05],
].map(([name, phone, b, rate]) => ({ name, phone, tradeBabId: bab(b), retentionRate: rate, acknowledgedDuplicatePhone: false }))));

console.log('employees:', await ensure('/api/employees', [
  ['كريم حسين عبد الله', '01044445501', 1, null, 'مهندس موقع', 'الإنشاءات', 'مهندس موقع'],
  ['ياسمين طارق', '01044445502', 1, null, 'محاسبة', 'المالية', 'محاسب أول'],
  ['عمرو سعيد', '01044445503', 1, null, 'مشتريات', 'المشتريات', 'مسؤول مشتريات'],
  ['هالة مصطفى', '01044445504', 1, null, 'موارد بشرية', 'الموارد البشرية', 'مسؤول موارد بشرية'],
  ['عم صلاح إبراهيم', '01044445505', 2, 'MAS', 'بناء', null, null],
  ['سيد جمعة', '01044445506', 2, 'FIN', 'نقاش', null, null],
  ['رمضان عبد الحميد', '01044445507', 2, 'ELE', 'كهربائي', null, null],
  ['محمد فتحي', '01044445508', 2, 'PLU', 'سباك', null, null],
].map(([fullName, phone, kind, b, specialty, department, jobTitle]) => ({
  fullName, phone, kind, babId: b ? bab(b) : null, specialty, department, jobTitle, acknowledgedDuplicatePhone: false,
}))));
console.log(`catalogue: ${created} created, ${skipped} existed, ${archived} archived, ${failed} failed`);
