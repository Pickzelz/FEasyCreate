# FEasyCreate

Editor tool kecil untuk membuat **satu set file sekaligus** dari preset yang bisa diatur — mis. saat bikin tanaman baru kamu butuh PlantData, ItemData benih, BuildingData, plus prefab-variant plant & benih. Alih-alih bikin satu per satu, atur sekali sebagai preset lalu klik **Create**.

## Instalasi
Package ini berupa git submodule. Di project Unity:

```bash
git submodule add git@github.com:Pickzelz/FEasyCreate.git Assets/modules/FEasyCreate
```

Buka Unity → biar meng-import & meng-compile.

## Cara pakai
1. Buka window: **Tools ▸ FEasyCreate**.
2. **＋ Add** preset (kiri). Preset bisa di-Duplicate / Delete (CRUD).
3. Isi preset (kanan):
   - **Base Name** — identitas set, mis. `berry`. Mengisi token `{name}`.
   - **Default Location** — folder default untuk file yang lokasinya dikosongkan.
   - **Files** — tambah baris per file:
     - **Kind** — `Auto` (tebak sendiri), `ScriptableObject`, `PrefabVariant`, atau `EmptyPrefab`.
     - **Script Class** — untuk ScriptableObject: pilih/drag file script-nya lewat object picker (mis. `PlantData`).
     - **Source Prefab** — untuk Prefab Variant (drag prefab sumber).
     - **Component** — (opsional) pilih/drag script Component untuk Empty GameObject prefab.
     - **Name Pattern** — pola nama, pakai `{name}`, mis. `{name}_plant` → `berry_plant`. Tanpa `{name}`, Base Name ditaruh di depan.
     - **File Location** — folder file ini (kosong = Default Location).
4. Klik **Create** — semua file dibuat, dinamai, dan ditaruh di folder yang benar. Aset yang dibuat langsung terseleksi di Project.

## Contoh preset "Tanaman baru"
| Kind | Class / Source | Name Pattern | Location |
|---|---|---|---|
| PrefabVariant | `plant_base.prefab` | `{name}` | `Assets/Game/Resources/prefab/Tanaman` |
| PrefabVariant | `seed_template.prefab` | `{name}_seed` | `Assets/Game/Resources/prefab/items/seeds` |
| ScriptableObject | `PlantData` | `{name}_plant` | `Assets/Game/Resources/data/Plants` |
| ScriptableObject | `ItemData` | `{name}_seed` | `Assets/Game/Resources/data/Item` |
| ScriptableObject | `BuildingData` | `{name}_plant` | `Assets/Game/Resources/data/Building` |

Base Name `berry` → menghasilkan `berry`, `berry_seed`, `berry_plant`, dst. dalam satu klik.

## Catatan
- Preset disimpan di **project** (`Assets/Editor/FEasyCreate/FEasyCreateSettings.asset`), bukan di dalam package — jadi package tetap generik/reusable dan presetmu ikut project.
- Versi ini **tidak** meng-auto-wire referensi antar file (mis. `PlantData.seedData`). Sambungkan sendiri di Inspector setelahnya.
- Class dipilih lewat object picker `MonoScript` (drag/pilih file script `.cs`) — tipe-nya diambil via `MonoScript.GetClass()`, jadi tak perlu ketik nama/namespace.
