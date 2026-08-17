# FEasyCreate

Editor tool kecil untuk membuat **satu set file sekaligus** dari preset yang bisa diatur — mis. saat bikin tanaman baru kamu butuh PlantData, ItemData benih, BuildingData, plus prefab-variant plant & benih. Atur sekali sebagai preset, lalu jalankan lewat **klik-kanan ▸ Create ▸ Easy Create**.

## Instalasi
Package ini berupa git submodule. Di project Unity:

```bash
git submodule add git@github.com:Pickzelz/FEasyCreate.git Assets/modules/FEasyCreate
```

Buka Unity → biarkan meng-import & meng-compile.

## Atur preset — Tools ▸ FEasyCreate
Window ini hanya untuk **mengatur** preset (tak ada tombol Create).

1. **＋ Add** preset (kiri). Bisa Duplicate / Delete (CRUD).
2. Isi preset (kanan):
   - **Base Name** — nilai awal token `{name}` (bisa ditimpa saat Create).
   - **Group in Folder** — ON = saat Create dibuatkan **folder** berisi semua file (nama folder = `{name}`); OFF = file dibuat langsung di folder yang diklik-kanan.
   - **Files** — tambah baris per file:
     - **Source** — object yang dibuat (satu picker untuk semua):
       - **Prefab** → **Prefab Variant**-nya · **Script SO** (mis. `PlantData`) → **aset SO baru** · **Script Component** → **prefab kosong + komponen** · **Aset lain** (SO/Material) → **salinan penuh**.
     - **Kind** — biasanya `Auto` (ditebak dari Source); bisa di-override.
     - **Name Pattern** — `{name}` = Base Name; `{edit}` = sama tapi menandai file ini yang **fokus rename pertama**. mis. `{edit}` atau `{name}_plant`.
3. Menu klik-kanan otomatis dibuat ulang saat window kehilangan fokus/ditutup — atau tekan **↻ Rebuild Create Menu**.

## Membuat file — klik-kanan di Project
**Project ▸ (folder tujuan) ▸ klik-kanan ▸ Create ▸ Easy Create ▸ [Nama Preset]**

- **Group in Folder ON** → muncul **folder baru** siap diberi nama; ketik nama → folder + semua file di dalamnya dibuat (nama folder mengisi `{name}`).
- **Group in Folder OFF** → muncul item **file fokus** (`{edit}`) siap diberi nama; ketik nama → semua file dibuat dengan `{name}` = nama itu.

File dibuat di **folder tempat kamu klik-kanan**.

## Contoh preset "Tanaman baru" (Group in Folder ON)
| Source (drop ini) | → dibuat | Name Pattern |
|---|---|---|
| `plant_base.prefab` | Prefab Variant | `{edit}` |
| `seed_template.prefab` | Prefab Variant | `{name}_seed` |
| `PlantData` (script) | SO baru | `{name}_plant` |
| `ItemData` (script) | SO baru | `{name}_seed` |
| `BuildingData` (script) | SO baru | `{name}_plant` |

Klik-kanan ▸ Create ▸ Easy Create ▸ Tanaman baru → ketik `berry` → folder **berry** berisi `berry`, `berry_seed`, `berry_plant`, dst.

## Catatan
- Preset & file menu tersimpan di **project** (`Assets/Editor/FEasyCreate/`), bukan di dalam package — jadi package tetap generik/reusable.
- **Tidak** ada auto-wiring referensi antar file — sambungkan sendiri di Inspector.
- Menu per-preset di-generate sebagai C# (`FEasyCreateMenu.gen.cs`); mengubah preset memicu recompile singkat.
