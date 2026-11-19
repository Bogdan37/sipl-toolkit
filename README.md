# SIPL Language Toolkit (.NET 8 / C#)

[![SIPL Toolkit CI & Release](https://github.com/Bogdan37/sipl-toolkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Bogdan37/sipl-toolkit/actions/workflows/ci.yml)

Відкритий модульний набір інструментів для навчальної імперативної мови програмування **SIPL** (*SImple Programming Language*), розроблений у межах проєктної роботи з дисципліни **«Теорія програмування»**.

## Обрані модулі та розрахунок балів

| ID | Модуль | Бали | Статус |
| :--- | :--- | :---: | :---: |
| `CORE-01` | Формальна специфікація SIPL (EBNF, лексика, $\ge 10$ валідних і $\ge 10$ невалідних прикладів) | 0.5 | Готово |
| `CORE-02` | Парсер та типізоване AST з позиціями у вихідному тексті (`SourceSpan`) | 1.5 | Готово |
| `CORE-03` | Інтерпретатор SIPL зі станом змінних, лімітом кроків та тестами ($\ge 80\%$) | 2.0 | Готово |
| `CORE-04` | Подієва модель виконання та JSON-трасування (`trace`) | 1.0 | Готово |
| `CORE-05` | CLI (`check`, `parse`, `run`, `trace`, `semantics`) зі структурованим JSON-режимом | 1.0 | Готово |
| `VIS-02` | Візуалізація AST (Text, JSON, DOT) та HTML-порівняння Parse Tree (CST) і AST | 1.0 | Готово |
| `SEM-01` | Автоматична побудова семантичного терму SIPL-програми в нотації курсу | 2.0 | Готово |
| `SEM-02` | Обчислення семантичного терму на стані з покроковим виводом застосованих правил | 2.0 | Готово |
| `REL-01` | CI pipeline на GitHub Actions | 0.5 | Готово |
| `REL-02` | GitHub Release + SemVer + CHANGELOG | 0.5 | Готово |
| **Разом** | *(підсумкова оцінка за проєкт обмежується **10.0 балами**)* | **12.0** | |

---

## Архітектура проєкту

```text
sipl-toolkit/
├── grammar/
│   └── sipl.ebnf                 # Формальна EBNF-граматика SIPL (CORE-01)
├── src/
│   ├── Sipl.Core/                # Незалежне ядро мови (бібліотека)
│   │   ├── Diagnostics/          # SourcePosition, SourceSpan, Diagnostic
│   │   ├── Syntax/               # AST-вузли, Token, Lexer, Recursive Descent Parser
│   │   ├── Execution/            # Interpreter, ExecutionOptions, TraceEvent
│   │   ├── Semantics/            # SemanticTermBuilder (SEM-01), SemanticEvaluator (SEM-02)
│   │   └── Visualization/        # ParseTreeBuilder, AstVisualizer (Text/DOT/JSON/HTML)
│   └── Sipl.Cli/                 # Консольний клієнт ядра (CORE-05)
├── tests/
│   └── Sipl.Core.Tests/          # Юніт-, golden- та інтеграційні тести xUnit
├── examples/
│   ├── valid/                    # 10 коректних SIPL-програм
│   └── invalid/                  # 10 програм із лексичними/синтаксичними помилками
├── .github/workflows/
│   └── ci.yml                    # GitHub Actions CI/CD (REL-01, REL-02)
├── CHANGELOG.md
└── README.md