#!/usr/bin/env python3
"""
lint_hu.py — Validación estructural (sin IA) de historias de usuario.

Uso:
    python scripts/lint_hu.py <archivo1.md> [archivo2.md ...]
    python scripts/lint_hu.py --scan          # busca automáticamente HU-*.md en el repo

Qué valida (determinístico, no semántico):
    - Presencia de todas las secciones obligatorias (## ...).
    - Que "Historia de usuario" contenga las tres partes Como/quiero/para.
    - Que "Alcance" mencione explícitamente incluye y no incluye.
    - Que existan al menos 2 criterios de aceptación (### CA-xx).
    - Que el número de "Reglas de negocio" numeradas no supere
      holgadamente al número de criterios de aceptación (heurística de
      cobertura: advierte, no falla, porque no siempre 1 regla = 1 CA).

Este script NO evalúa si el CONTENIDO tiene sentido de negocio — esa
parte la hace el skill `hu-validation` con un agente. Este script es la
primera línea de defensa, rápida y gratis, que corre en CI antes de
gastar ningún ciclo de agente en una HU que ni siquiera tiene la forma
correcta.
"""

import re
import sys
from pathlib import Path

REQUIRED_SECTIONS = [
    "Historia de usuario",
    "Alcance",
    "Datos de entrada",
    "Valores generados por el sistema",
    "Flujo principal",
    "Reglas de negocio",
    "Errores que deben comunicarse",
    "Criterios de aceptación",
    "Trazabilidad",
]

EXCLUDE_PATTERNS = ["HU-TEMPLATE.md"]


def find_section_headers(text: str) -> set[str]:
    headers = re.findall(r"^##\s+(.+?)\s*$", text, flags=re.MULTILINE)
    return {h.strip() for h in headers}


def lint_file(path: Path) -> list[str]:
    errors: list[str] = []
    text = path.read_text(encoding="utf-8")

    headers = find_section_headers(text)
    missing = [s for s in REQUIRED_SECTIONS if s not in headers]
    if missing:
        errors.append(f"Faltan secciones obligatorias: {', '.join(missing)}")

    if "Historia de usuario" in headers:
        historia_match = re.search(
            r"##\s+Historia de usuario\s*\n(.*?)(?=\n##\s|\Z)", text, flags=re.DOTALL
        )
        historia_text = historia_match.group(1) if historia_match else ""
        for palabra in ["Como", "quiero", "para"]:
            if palabra.lower() not in historia_text.lower():
                errors.append(
                    f"La sección 'Historia de usuario' no contiene la palabra clave '{palabra}'"
                )

    if "Alcance" in headers:
        alcance_match = re.search(
            r"##\s+Alcance\s*\n(.*?)(?=\n##\s|\Z)", text, flags=re.DOTALL
        )
        alcance_text = (alcance_match.group(1) if alcance_match else "").lower()
        if "incluye" not in alcance_text:
            errors.append("La sección 'Alcance' no menciona qué SÍ incluye")
        if "no incluye" not in alcance_text and "no se incluye" not in alcance_text:
            errors.append("La sección 'Alcance' no menciona qué NO incluye")

    ca_count = len(re.findall(r"^###\s+CA-\d+", text, flags=re.MULTILINE))
    if ca_count < 2:
        errors.append(
            f"Se encontraron solo {ca_count} criterios de aceptación (### CA-xx); se esperan al menos 2 (un caso exitoso y uno de fallo)"
        )

    reglas_match = re.search(
        r"##\s+Reglas de negocio\s*\n(.*?)(?=\n##\s|\Z)", text, flags=re.DOTALL
    )
    if reglas_match:
        reglas_count = len(re.findall(r"^\d+\.\s", reglas_match.group(1), flags=re.MULTILINE))
        if reglas_count > 0 and ca_count < reglas_count:
            errors.append(
                f"ADVERTENCIA: hay {reglas_count} reglas de negocio pero solo {ca_count} criterios de aceptación — "
                f"revisa que cada regla que pueda violarse tenga su CA de fallo correspondiente"
            )

    return errors


def discover_hu_files(root: Path) -> list[Path]:
    candidates = list(root.rglob("HU-*.md"))
    return [
        p for p in candidates
        if not any(excl in p.name for excl in EXCLUDE_PATTERNS)
    ]


def main() -> int:
    args = sys.argv[1:]

    if not args or args == ["--scan"]:
        repo_root = Path(__file__).resolve().parent.parent
        files = discover_hu_files(repo_root)
        if not files:
            print("No se encontraron archivos HU-*.md para validar.")
            return 0
    else:
        files = [Path(a) for a in args]

    had_errors = False
    for path in files:
        if not path.exists():
            print(f"::error::No existe el archivo {path}")
            had_errors = True
            continue

        errors = lint_file(path)
        warnings = [e for e in errors if e.startswith("ADVERTENCIA")]
        real_errors = [e for e in errors if not e.startswith("ADVERTENCIA")]

        if not errors:
            print(f"OK  {path}")
            continue

        for w in warnings:
            print(f"::warning file={path}::{w}")
        for e in real_errors:
            print(f"::error file={path}::{e}")

        if real_errors:
            had_errors = True

    return 1 if had_errors else 0


if __name__ == "__main__":
    sys.exit(main())
