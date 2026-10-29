#!/usr/bin/env python3
"""Kiểm tra kiến trúc layer của project (không cần Flutter SDK).

1. Mọi import/export tương đối hoặc package:smart_drone_delivery/... phải trỏ tới file tồn tại.
2. Hướng phụ thuộc giữa các layer phải đúng:

     core          -> core
     domain        -> core, domain          (và không dùng flutter/dio/riverpod/go_router)
     data          -> core, domain, data
     presentation  -> core, domain, data, presentation, routes
     routes        -> core, presentation, routes
     app / main_*  -> tất cả

Chạy:  python3 tool/check_architecture.py     (thoát mã 1 nếu có vi phạm)
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LIB = ROOT / "lib"
PKG = "smart_drone_delivery"

ALLOWED = {
    "core": {"core"},
    "domain": {"core", "domain"},
    "data": {"core", "domain", "data"},
    "presentation": {"core", "domain", "data", "presentation", "routes"},
    "routes": {"core", "presentation", "routes"},
}
PURE_FORBIDDEN = (
    "package:flutter/", "package:flutter_riverpod/", "package:dio/",
    "package:flutter_secure_storage/", "package:go_router/",
)
IMPORT_RE = re.compile(r"""^\s*(?:import|export)\s+['"]([^'"]+)['"]""", re.M)


def layer_of(path: pathlib.Path):
    rel = path.relative_to(LIB)
    return rel.parts[0] if len(rel.parts) > 1 else "root"


def main() -> int:
    errors, checked = [], 0
    files = sorted(LIB.rglob("*.dart")) + sorted((ROOT / "test").rglob("*.dart"))
    for f in files:
        in_lib = LIB in f.parents
        src_layer = layer_of(f) if in_lib else "test"
        for uri in IMPORT_RE.findall(f.read_text(encoding="utf-8")):
            checked += 1
            where = f"{f.relative_to(ROOT)}"
            if uri.startswith("dart:"):
                continue
            if uri.startswith("package:"):
                if uri.startswith(f"package:{PKG}/"):
                    target = LIB / uri[len(f"package:{PKG}/"):]
                else:
                    if src_layer == "domain" and uri.startswith(PURE_FORBIDDEN):
                        errors.append(f"{where}: domain phải là Dart thuần, không import '{uri}'")
                    continue
            else:
                target = (f.parent / uri).resolve()

            if not target.exists():
                errors.append(f"{where}: import không tồn tại -> '{uri}'")
                continue
            if not in_lib or LIB not in target.parents:
                continue
            dst_layer = layer_of(target)
            if src_layer in ALLOWED and dst_layer not in ALLOWED[src_layer] and dst_layer != "root":
                errors.append(f"{where}: layer '{src_layer}' không được phụ thuộc '{dst_layer}' ('{uri}')")

    if errors:
        print("VI PHẠM KIẾN TRÚC:")
        for e in errors:
            print("  -", e)
        return 1
    print(f"OK: {len(files)} file, {checked} import - đúng kiến trúc layer.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
