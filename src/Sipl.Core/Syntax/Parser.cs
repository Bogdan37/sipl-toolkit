using Sipl.Core.Diagnostics;

namespace Sipl.Core.Syntax;

public sealed record ParseResult(
    ProgramNode? Program,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsSuccess => Program is not null && Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);
}

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics;
    private int _position;

    public Parser(IReadOnlyList<Token> tokens, IEnumerable<Diagnostic>? initialDiagnostics = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _diagnostics = initialDiagnostics is not null ? [.. initialDiagnostics] : [];
    }

    public static ParseResult ParseSource(string source)
    {
        var lexer = new Lexer(source);
        var lexResult = lexer.Tokenize();
        var parser = new Parser(lexResult.Tokens, lexResult.Diagnostics);
        return parser.ParseProgram();
    }

    private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];

    private Token Peek(int offset = 1) =>
        _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens[^1];

    private Token Previous => _position > 0 ? _tokens[_position - 1] : _tokens[0];

    private bool Check(TokenKind kind) => Current.Kind == kind;

    private bool Match(params TokenKind[] kinds)
    {
        foreach (var kind in kinds)
        {
            if (Check(kind))
            {
                _position++;
                return true;
            }
        }
        return false;
    }

    private Token Consume(TokenKind kind, string code, string errorMessage)
    {
        if (Check(kind))
        {
            var token = Current;
            _position++;
            return token;
        }

        _diagnostics.Add(new Diagnostic(code, errorMessage, Current.Span));
        throw new ParseException();
    }

    public ParseResult ParseProgram()
    {
        if (Check(TokenKind.EndOfFile))
        {
            _diagnostics.Add(new Diagnostic(
                "SYN001",
                "Програма не може бути порожньою. Очікувався оператор (наприклад, 'skip' або присвоювання).",
                Current.Span));
            return new ParseResult(null, _diagnostics);
        }

        try
        {
            var body = ParseSequence();

            if (!Check(TokenKind.EndOfFile))
            {
                _diagnostics.Add(new Diagnostic(
                    "SYN002",
                    $"Неочікуваний токен '{Current.Lexeme}' після завершення програми.",
                    Current.Span));
                return new ParseResult(null, _diagnostics);
            }

            var program = new ProgramNode(body, body.Span);
            bool hasErrors = _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            return new ParseResult(hasErrors ? null : program, _diagnostics);
        }
        catch (ParseException)
        {
            return new ParseResult(null, _diagnostics);
        }
    }

    private Stmt ParseSequence()
    {
        var statements = new List<Stmt> { ParseSimpleStatement() };

        while (Match(TokenKind.Semicolon))
        {
            if (Check(TokenKind.End))
                break;

            if (Check(TokenKind.Semicolon))
            {
                _diagnostics.Add(new Diagnostic(
                    "SYN003",
                    "Зайва крапка з комою ';'. Порожні оператори між ';' не допускаються.",
                    Current.Span));
                throw new ParseException();
            }

            statements.Add(ParseSimpleStatement());
        }

        if (statements.Count == 1)
            return statements[0];

        var span = SourceSpan.Combine(statements[0].Span, statements[^1].Span);
        return new SequenceStmt(statements, span);
    }

    private Stmt ParseSimpleStatement()
    {
        return Current.Kind switch
        {
            TokenKind.Skip => ParseSkip(),
            TokenKind.Identifier => ParseAssign(),
            TokenKind.If => ParseIf(),
            TokenKind.While => ParseWhile(),
            TokenKind.Begin => ParseBlock(),
            _ => ReportUnexpectedStatement()
        };
    }

    private Stmt ReportUnexpectedStatement()
    {
        string found = Current.Kind == TokenKind.EndOfFile ? "кінець файлу" : $"'{Current.Lexeme}'";
        _diagnostics.Add(new Diagnostic(
            "SYN004",
            $"Очікувався оператор ('skip', присвоювання, 'if', 'while' або 'begin'), але знайдено {found}.",
            Current.Span));
        throw new ParseException();
    }

    private SkipStmt ParseSkip()
    {
        var token = Consume(TokenKind.Skip, "SYN005", "Очікувалося ключове слово 'skip'.");
        return new SkipStmt(token.Span);
    }

    private AssignStmt ParseAssign()
    {
        var idToken = Consume(TokenKind.Identifier, "SYN006", "Очікувався ідентифікатор змінної.");
        Consume(TokenKind.Assign, "SYN007", $"Очікувався оператор присвоювання ':=' після змінної '{idToken.Lexeme}'.");
        var expr = ParseArithExpr();
        var fullSpan = SourceSpan.Combine(idToken.Span, expr.Span);
        return new AssignStmt(idToken.Lexeme, idToken.Span, expr, fullSpan);
    }

    private IfStmt ParseIf()
    {
        var ifToken = Consume(TokenKind.If, "SYN008", "Очікувалося ключове слово 'if'.");
        var condition = ParseBoolExpr();
        Consume(TokenKind.Then, "SYN009", "Очікувалося ключове слово 'then' після умови 'if'.");
        var thenBranch = ParseSimpleStatement();

        Stmt? elseBranch = null;
        if (Match(TokenKind.Else))
        {
            elseBranch = ParseSimpleStatement();
        }

        var endSpan = elseBranch?.Span ?? thenBranch.Span;
        return new IfStmt(condition, thenBranch, elseBranch, SourceSpan.Combine(ifToken.Span, endSpan));
    }

    private WhileStmt ParseWhile()
    {
        var whileToken = Consume(TokenKind.While, "SYN010", "Очікувалося ключове слово 'while'.");
        var condition = ParseBoolExpr();
        Consume(TokenKind.Do, "SYN011", "Очікувалося ключове слово 'do' після умови 'while'.");
        var body = ParseSimpleStatement();
        return new WhileStmt(condition, body, SourceSpan.Combine(whileToken.Span, body.Span));
    }

    private BlockStmt ParseBlock()
    {
        var beginToken = Consume(TokenKind.Begin, "SYN012", "Очікувалося ключове слово 'begin'.");

        if (Check(TokenKind.End))
        {
            _diagnostics.Add(new Diagnostic(
                "SYN013",
                "Блок 'begin ... end' не може бути порожнім (використайте 'skip').",
                SourceSpan.Combine(beginToken.Span, Current.Span)));
            throw new ParseException();
        }

        var inner = ParseSequence();
        var endToken = Consume(TokenKind.End, "SYN014", "Очікувалося ключове слово 'end' для закриття блоку 'begin'.");
        return new BlockStmt(inner, SourceSpan.Combine(beginToken.Span, endToken.Span));
    }


    private ArithExpr ParseArithExpr()
    {
        var left = ParseTerm();

        while (Check(TokenKind.Plus) || Check(TokenKind.Minus))
        {
            var opToken = Current;
            _position++;
            var op = opToken.Kind == TokenKind.Plus
                ? BinaryArithOperator.Add
                : BinaryArithOperator.Subtract;
            var right = ParseTerm();
            left = new BinaryArithExpr(left, op, right, SourceSpan.Combine(left.Span, right.Span));
        }

        return left;
    }

    private ArithExpr ParseTerm()
    {
        var left = ParseFactor();

        while (Check(TokenKind.Star) || Check(TokenKind.Slash) || Check(TokenKind.Percent))
        {
            var opToken = Current;
            _position++;
            var op = opToken.Kind switch
            {
                TokenKind.Star => BinaryArithOperator.Multiply,
                TokenKind.Slash => BinaryArithOperator.Divide,
                TokenKind.Percent => BinaryArithOperator.Modulo,
                _ => throw new InvalidOperationException()
            };
            var right = ParseFactor();
            left = new BinaryArithExpr(left, op, right, SourceSpan.Combine(left.Span, right.Span));
        }

        return left;
    }

    private ArithExpr ParseFactor()
    {
        if (Check(TokenKind.Minus))
        {
            var minusToken = Current;
            _position++;
            var operand = ParseFactor();
            return new UnaryArithExpr(
                UnaryArithOperator.Negate,
                operand,
                SourceSpan.Combine(minusToken.Span, operand.Span));
        }

        return ParsePrimary();
    }

    private ArithExpr ParsePrimary()
    {
        if (Check(TokenKind.Number))
        {
            var numToken = Current;
            _position++;
            return new NumberExpr(numToken.IntValue ?? 0, numToken.Span);
        }

        if (Check(TokenKind.Identifier))
        {
            var idToken = Current;
            _position++;
            return new VariableExpr(idToken.Lexeme, idToken.Span);
        }

        if (Check(TokenKind.LParen))
        {
            var lparen = Current;
            _position++;
            var expr = ParseArithExpr();
            var rparen = Consume(TokenKind.RParen, "SYN015", "Очікувалася закриваюча дужка ')' в арифметичному виразі.");
            return expr with { Span = SourceSpan.Combine(lparen.Span, rparen.Span) };
        }

        string found = Current.Kind == TokenKind.EndOfFile ? "кінець файлу" : $"'{Current.Lexeme}'";
        _diagnostics.Add(new Diagnostic(
            "SYN016",
            $"Очікувався арифметичний операнд (число, змінна або вираз у дужках), але знайдено {found}.",
            Current.Span));
        throw new ParseException();
    }


    private BoolExpr ParseBoolExpr()
    {
        var left = ParseBoolTerm();

        while (Match(TokenKind.Or))
        {
            var right = ParseBoolTerm();
            left = new LogicalExpr(left, LogicalOperator.Or, right, SourceSpan.Combine(left.Span, right.Span));
        }

        return left;
    }

    private BoolExpr ParseBoolTerm()
    {
        var left = ParseBoolFactor();

        while (Match(TokenKind.And))
        {
            var right = ParseBoolFactor();
            left = new LogicalExpr(left, LogicalOperator.And, right, SourceSpan.Combine(left.Span, right.Span));
        }

        return left;
    }

    private BoolExpr ParseBoolFactor()
    {
        if (Check(TokenKind.Not))
        {
            var notToken = Current;
            _position++;
            var operand = ParseBoolFactor();
            return new NotExpr(operand, SourceSpan.Combine(notToken.Span, operand.Span));
        }

        if (Check(TokenKind.True))
        {
            var token = Current;
            _position++;
            return new BoolLiteralExpr(true, token.Span);
        }

        if (Check(TokenKind.False))
        {
            var token = Current;
            _position++;
            return new BoolLiteralExpr(false, token.Span);
        }

        if (Check(TokenKind.LParen) && LooksLikeParenthesizedBoolExpr())
        {
            var lparen = Current;
            _position++;
            var inner = ParseBoolExpr();
            var rparen = Consume(TokenKind.RParen, "SYN017", "Очікувалася закриваюча дужка ')' у логічному виразі.");
            return inner with { Span = SourceSpan.Combine(lparen.Span, rparen.Span) };
        }

        return ParseComparison();
    }


    private bool LooksLikeParenthesizedBoolExpr()
    {
        int depth = 0;
        for (int i = _position; i < _tokens.Count; i++)
        {
            var kind = _tokens[i].Kind;
            if (kind == TokenKind.LParen)
            {
                depth++;
            }
            else if (kind == TokenKind.RParen)
            {
                depth--;
                if (depth == 0)
                {
                    var nextKind = i + 1 < _tokens.Count ? _tokens[i + 1].Kind : TokenKind.EndOfFile;

                    return nextKind is not (
                        TokenKind.Equal or TokenKind.NotEqual or
                        TokenKind.Less or TokenKind.LessEqual or
                        TokenKind.Greater or TokenKind.GreaterEqual or
                        TokenKind.Plus or TokenKind.Minus or
                        TokenKind.Star or TokenKind.Slash or TokenKind.Percent);
                }
            }
            else if (kind == TokenKind.EndOfFile)
            {
                return false;
            }
        }
        return false;
    }

    private ComparisonExpr ParseComparison()
    {
        var left = ParseArithExpr();

        var op = Current.Kind switch
        {
            TokenKind.Equal => RelOperator.Equal,
            TokenKind.NotEqual => RelOperator.NotEqual,
            TokenKind.Less => RelOperator.Less,
            TokenKind.LessEqual => RelOperator.LessOrEqual,
            TokenKind.Greater => RelOperator.Greater,
            TokenKind.GreaterEqual => RelOperator.GreaterOrEqual,
            _ => ReportExpectedRelOp()
        };

        _position++;
        var right = ParseArithExpr();
        return new ComparisonExpr(left, op, right, SourceSpan.Combine(left.Span, right.Span));
    }

    private RelOperator ReportExpectedRelOp()
    {
        string found = Current.Kind == TokenKind.EndOfFile ? "кінець файлу" : $"'{Current.Lexeme}'";
        _diagnostics.Add(new Diagnostic(
            "SYN018",
            $"Очікувався оператор порівняння ('=', '<>', '!=', '<', '<=', '>', '>='), але знайдено {found}.",
            Current.Span));
        throw new ParseException();
    }

    private sealed class ParseException : Exception;
}