# Changelog

Усі суттєві зміни в проєкті **SIPL Language Toolkit** документуються у цьому файлі.
Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.0.0/), а версіонування відповідає [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-19

### Added
- **CORE-01**: Формальна EBNF-граматика мови SIPL (`grammar/sipl.ebnf`) та набір із 20 тестових програм (`examples/valid` та `examples/invalid`).
- **CORE-02**: Лексичний аналізатор (`Lexer`), парсер рекурсивного спуску (`Parser`), незмінне типізоване AST та система діагностики з точними координатами `SourceSpan`.
- **CORE-03**: Інтерпретатор SIPL (`Interpreter`) з підтримкою змінних, присвоювання, композиції, `if/then/else`, `while/do`, `begin/end`, `skip` та захистом від нескінченних циклів (`MaxSteps`).
- **CORE-04**: Подієва модель трасування виконання (`BeforeStatement`, `AfterStatement`, `VariableChanged`, `ConditionChecked`, `Finished`) з експортом у JSON.
- **CORE-05**: Консольний застосунок `Sipl.Cli` з командами `check`, `parse`, `run`, `trace`, `semantics`, підтримкою `--json` та стабільними кодами завершення.
- **VIS-02**: Генератор представлень AST у форматах дерева для консолі (Text), Graphviz (`DOT`) та `JSON`.
- **SEM-01**: Рекурсивний побудовник формальних семантичних термів (`SemanticTermBuilder`) у нотації курсу (`id`, `assign`, `∘`, `cond`, `fix`).
- **SEM-02**: Обчислювач семантичних термів (`SemanticEvaluator`) на початковому стані з покроковим виводом застосованих правил переходу та обробкою невизначеності (`⊥`).
- **REL-01 / REL-02**: Конфігурація GitHub Actions CI/CD для автоматичної збірки, тестування та публікації релізів.