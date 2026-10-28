#!/usr/bin/env python3
"""Create deterministic Unity .meta files for any asset under Assets/ that lacks one.

GUIDs are derived from the asset's relative path so repeated runs are stable and
the files can be committed without opening the Unity Editor. Unity accepts these
metas as-is; if Unity later rewrites one (e.g. adds importer settings) that is fine.

Usage: python3 -I Tools/gen_meta.py [project_root]
"""
import hashlib, os, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

def guid_for(rel):
    return hashlib.md5(("ShadowSteps:" + rel).encode("utf-8")).hexdigest()

def body(path):
    if os.path.isdir(path):
        return "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + TAIL
    ext = os.path.splitext(path)[1].lower()
    if ext == ".cs":
        return ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
                "  executionOrder: 0\n  icon: {instanceID: 0}\n" + TAIL)
    if ext == ".asmdef":
        return "AssemblyDefinitionImporter:\n  externalObjects: {}\n" + TAIL
    if ext in (".json", ".md", ".txt", ".csv", ".xml"):
        return "TextScriptImporter:\n  externalObjects: {}\n" + TAIL
    if ext == ".asset":
        return "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n" + TAIL
    if ext == ".mat":
        return "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n" + TAIL
    if ext == ".prefab":
        return "PrefabImporter:\n  externalObjects: {}\n" + TAIL
    if ext == ".unity":
        return "DefaultImporter:\n  externalObjects: {}\n" + TAIL
    return None

created = 0
for dirpath, dirnames, filenames in os.walk(ASSETS):
    dirnames[:] = [d for d in dirnames if not d.startswith(".")]
    entries = [os.path.join(dirpath, d) for d in dirnames] + [os.path.join(dirpath, f) for f in filenames if not f.endswith(".meta") and not f.startswith(".")]
    for p in entries:
        meta = p + ".meta"
        if os.path.exists(meta):
            continue
        b = body(p)
        rel = os.path.relpath(p, ROOT).replace(os.sep, "/")
        if b is None:
            print("SKIP (unknown importer, let Unity create it):", rel)
            continue
        with open(meta, "w") as f:
            f.write("fileFormatVersion: 2\nguid: %s\n%s" % (guid_for(rel), b))
        created += 1
print("meta files created:", created)
