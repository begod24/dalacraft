# DalaCraft

Воксельная песочница в казахской степи (Unity 6 / URP). Все модели и текстуры делаются в Blender.

## Структура
```
dalacraft/
├── Unity/      Unity-проект (открывать эту папку в Unity Hub)
│   ├── Assets/          скрипты (Assets/Scripts), шейдеры, модели и текстуры (Assets/Resources)
│   ├── Packages/
│   └── ProjectSettings/
├── Blender/
│   ├── DalaCraft_Assets.blend   исходник всех моделей
│   └── tools/                   скрипты сборки и экспорта (km_tools.py — общий хелпер)
└── Docs/
    └── ASSETS_PLAN.md   план ассетов и TODO
```

## Первый запуск
```bash
git lfs install
git lfs pull
```
Затем в Unity Hub → Add → папка `Unity/`.

Бинарные файлы (`.blend`, `.fbx`, `.png`, аудио, шрифты) хранятся в Git LFS — см. `.gitattributes`.
