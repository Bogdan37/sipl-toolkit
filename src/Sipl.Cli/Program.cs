using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sipl.Core.Diagnostics;
using Sipl.Core.Execution;
using Sipl.Core.Semantics;
using Sipl.Core.Syntax;
using Sipl.Core.Visualization;

namespace Sipl.Cli;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        string[] rest = args[1..];

        return command switch
        {
            "check" => HandleCheck(rest),
            "parse" => HandleParse(rest),
            "run" => HandleRun(rest),
            "trace" => HandleTrace(rest),
            "semantics" => HandleSemantics(rest),
            _ => FailCli($"Невідома команда '{args[0]}'. Використайте --help для перегляду доступних команд.")
        };
    }

    private static int HandleCheck(string[] args)
    {
        if (!TryParseCommonArgs(args, out var opts, out var error))
            return FailCli(error!);

        var parseResult = Parser.ParseSource(opts.Source!);

        if (opts.Json)
        {
            var payload = new
            {
                command = "check",
                file = opts.FilePath,
                isValid = parseResult.IsSuccess,
                diagnostics = parseResult.Diagnostics
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else if (parseResult.IsSuccess)
        {
            Console.WriteLine($"OK: '{opts.FilePath}' не містить лексичних чи синтаксичних помилок.");
        }
        else
        {
            PrintDiagnostics(parseResult.Diagnostics);
        }

        return parseResult.IsSuccess ? 0 : 2;
    }

    private static int HandleParse(string[] args)
    {
        if (!TryParseCommonArgs(args, out var opts, out var error))
            return FailCli(error!);

        string format = opts.Format ?? (opts.Json ? "json" : "text");
        if (format is not ("text" or "json" or "dot" or "cst" or "compare"))
            return FailCli($"Непідтримуваний формат '{format}'. Доступні: text, json, dot, cst, compare.");

        var parseResult = Parser.ParseSource(opts.Source!);
        if (!parseResult.IsSuccess)
        {
            if (opts.Json || format == "json")
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "parse",
                    file = opts.FilePath,
                    isSuccess = false,
                    diagnostics = parseResult.Diagnostics
                }, JsonOptions));
            }
            else
            {
                PrintDiagnostics(parseResult.Diagnostics);
            }
            return 2;
        }

        string output = format switch
        {
            "dot" => AstVisualizer.ToDot(parseResult.Program!),
            "json" => AstVisualizer.ToJson(parseResult.Program!),
            "cst" => ParseTreeBuilder.ToTextTree(ParseTreeBuilder.Build(parseResult.Program!)),
            "compare" => AstVisualizer.ToComparisonHtml(parseResult.Program!, opts.Source!),
            _ => AstVisualizer.ToTextTree(parseResult.Program!)
        };

        Console.WriteLine(output);
        return 0;
    }

    private static int HandleRun(string[] args)
    {
        if (!TryParseCommonArgs(args, out var opts, out var error))
            return FailCli(error!);

        var parseResult = Parser.ParseSource(opts.Source!);
        if (!parseResult.IsSuccess)
        {
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "run",
                    file = opts.FilePath,
                    status = "ParseError",
                    diagnostics = parseResult.Diagnostics
                }, JsonOptions));
            }
            else
            {
                PrintDiagnostics(parseResult.Diagnostics);
            }
            return 2;
        }

        var interpreter = new Interpreter(new ExecutionOptions
        {
            MaxSteps = opts.MaxSteps,
            RecordTrace = false,
            InitialState = opts.InitialVariables
        });

        var result = interpreter.Execute(parseResult.Program!);

        if (opts.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                command = "run",
                file = opts.FilePath,
                status = result.Status,
                stepsExecuted = result.StepsExecuted,
                finalState = result.FinalState,
                diagnostics = result.Diagnostics
            }, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Статус: {result.Status} (виконано кроків: {result.StepsExecuted})");
            Console.WriteLine($"Кінцевий стан: {FormatState(result.FinalState)}");
            if (result.Diagnostics.Count > 0)
                PrintDiagnostics(result.Diagnostics);
        }

        return result.IsSuccess ? 0 : 3;
    }

    private static int HandleTrace(string[] args)
    {
        if (!TryParseCommonArgs(args, out var opts, out var error))
            return FailCli(error!);

        var parseResult = Parser.ParseSource(opts.Source!);
        if (!parseResult.IsSuccess)
        {
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "trace",
                    file = opts.FilePath,
                    status = "ParseError",
                    diagnostics = parseResult.Diagnostics
                }, JsonOptions));
            }
            else
            {
                PrintDiagnostics(parseResult.Diagnostics);
            }
            return 2;
        }

        var interpreter = new Interpreter(new ExecutionOptions
        {
            MaxSteps = opts.MaxSteps,
            RecordTrace = true,
            InitialState = opts.InitialVariables
        });

        var result = interpreter.Execute(parseResult.Program!);

        if (opts.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                command = "trace",
                file = opts.FilePath,
                status = result.Status,
                stepsExecuted = result.StepsExecuted,
                finalState = result.FinalState,
                trace = result.Trace,
                diagnostics = result.Diagnostics
            }, JsonOptions));
        }
        else
        {
            foreach (var ev in result.Trace)
            {
                string extra = ev.Description is not null ? $" | {ev.Description}" : string.Empty;
                Console.WriteLine($"[Step {ev.Step}] {ev.Kind} ({ev.StatementType} @ {ev.Span}){extra} -> {FormatState(ev.StateSnapshot)}");
            }
            if (result.Diagnostics.Count > 0)
                PrintDiagnostics(result.Diagnostics);
        }

        return result.IsSuccess ? 0 : 3;
    }

    private static int HandleSemantics(string[] args)
    {
        if (!TryParseCommonArgs(args, out var opts, out var error))
            return FailCli(error!);

        var parseResult = Parser.ParseSource(opts.Source!);
        if (!parseResult.IsSuccess)
        {
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "semantics",
                    file = opts.FilePath,
                    status = "ParseError",
                    diagnostics = parseResult.Diagnostics
                }, JsonOptions));
            }
            else
            {
                PrintDiagnostics(parseResult.Diagnostics);
            }
            return 2;
        }

        var term = SemanticTermBuilder.Build(parseResult.Program!);
        var evaluator = new SemanticEvaluator(maxUnfoldings: opts.MaxSteps);
        var evalResult = evaluator.Evaluate(term, opts.InitialVariables);

        if (opts.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                command = "semantics",
                file = opts.FilePath,
                semanticTerm = term.ToNotation(),
                isDefined = evalResult.IsDefined,
                finalState = evalResult.FinalState,
                undefinedReason = evalResult.UndefinedReason,
                steps = evalResult.Steps
            }, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Семантичний терм:\n  S[[P]] = {term.ToNotation()}\n");
            Console.WriteLine("Кроки обчислення:");
            foreach (var step in evalResult.Steps)
            {
                Console.WriteLine($"  {step.StepNumber}. [{step.RuleName}] {step.Explanation}");
            }
            Console.WriteLine();
            Console.WriteLine(evalResult.IsDefined
                ? $"Результат: {FormatState(evalResult.FinalState!)}"
                : $"Результат: ⊥ ({evalResult.UndefinedReason})");
        }

        return evalResult.IsDefined ? 0 : 3;
    }

    private static bool TryParseCommonArgs(string[] args, out CliOptions options, out string? error)
    {
        options = default!;
        error = null;

        string? filePath = null;
        bool json = false;
        string? format = null;
        int maxSteps = 10_000;
        var vars = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--json")
            {
                json = true;
            }
            else if (arg == "--format")
            {
                if (i + 1 >= args.Length)
                {
                    error = "Прапорець '--format' вимагає значення (text, json, dot, cst або compare).";
                    return false;
                }
                format = args[++i].ToLowerInvariant();
            }
            else if (arg == "--max-steps")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[++i], out maxSteps) || maxSteps <= 0)
                {
                    error = "Прапорець '--max-steps' вимагає додатне ціле число.";
                    return false;
                }
            }
            else if (arg == "--var")
            {
                if (i + 1 >= args.Length)
                {
                    error = "Прапорець '--var' вимагає пару виду NAME=VALUE (наприклад, --var X=5).";
                    return false;
                }
                string pair = args[++i];
                int eqIdx = pair.IndexOf('=');
                if (eqIdx <= 0 || !int.TryParse(pair[(eqIdx + 1)..], out int val))
                {
                    error = $"Некоректний формат змінної '{pair}'. Очікується NAME=INT.";
                    return false;
                }
                vars[pair[..eqIdx]] = val;
            }
            else if (arg.StartsWith('-'))
            {
                error = $"Невідомий параметр '{arg}'.";
                return false;
            }
            else if (filePath is null)
            {
                filePath = arg;
            }
            else
            {
                error = $"Зайвий позиційний аргумент '{arg}'.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            error = "Не вказано шлях до .sipl файлу.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            error = $"Файл '{filePath}' не знайдено.";
            return false;
        }

        options = new CliOptions(filePath, File.ReadAllText(filePath), json, format, maxSteps, vars);
        return true;
    }

    private static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (var d in diagnostics)
            Console.Error.WriteLine(d);
    }

    private static string FormatState(IReadOnlyDictionary<string, int> state)
    {
        if (state.Count == 0)
            return "{}";
        var items = state.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} = {k.Value}");
        return "{ " + string.Join(", ", items) + " }";
    }

    private static int FailCli(string message)
    {
        Console.Error.WriteLine($"Помилка CLI: {message}");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            SIPL Language Toolkit CLI
            Використання:
              sipl check <file.sipl> [--json]
              sipl parse <file.sipl> [--format text|json|dot|cst|compare] [--json]
              sipl run <file.sipl> [--var X=10 ...] [--max-steps 10000] [--json]
              sipl trace <file.sipl> [--var X=10 ...] [--max-steps 10000] [--json]
              sipl semantics <file.sipl> [--var X=10 ...] [--max-steps 1000] [--json]

            Коди завершення:
              0 - успіх
              1 - помилка аргументів CLI або файл не знайдено
              2 - лексична або синтаксична помилка
              3 - помилка виконання або невизначена семантика (⊥)
            """);
    }

    private sealed record CliOptions(
        string FilePath,
        string Source,
        bool Json,
        string? Format,
        int MaxSteps,
        IReadOnlyDictionary<string, int> InitialVariables);
}