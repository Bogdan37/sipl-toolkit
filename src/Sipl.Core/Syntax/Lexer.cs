using Sipl.Core.Diagnostics;

namespace Sipl.Core.Syntax;

public sealed record LexResult(
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed class Lexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["skip"] = TokenKind.Skip,
        ["begin"] = TokenKind.Begin,
        ["end"] = TokenKind.End,
        ["if"] = TokenKind.If,
        ["then"] = TokenKind.Then,
        ["else"] = TokenKind.Else,
        ["while"] = TokenKind.While,
        ["do"] = TokenKind.Do,
        ["true"] = TokenKind.True,
        ["false"] = TokenKind.False,
        ["and"] = TokenKind.And,
        ["or"] = TokenKind.Or,
        ["not"] = TokenKind.Not
    };

    private readonly string _source;
    private readonly List<Diagnostic> _diagnostics = [];
    private int _offset;
    private int _line = 1;
    private int _column = 1;

    public Lexer(string source)
    {
        _source = source ?? string.Empty;
    }

    public LexResult Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd)
        {
            SkipTrivia();
            if (IsAtEnd)
                break;

            tokens.Add(ReadToken());
        }

        var eofPos = CurrentPosition;
        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, new SourceSpan(eofPos, eofPos)));

        return new LexResult(tokens, _diagnostics);
    }

    private SourcePosition CurrentPosition => new(_line, _column, _offset);

    private bool IsAtEnd => _offset >= _source.Length;

    private char Current => IsAtEnd ? '\0' : _source[_offset];

    private char Peek(int lookahead = 1) =>
        _offset + lookahead >= _source.Length ? '\0' : _source[_offset + lookahead];

    private char Advance()
    {
        var ch = _source[_offset++];
        if (ch == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return ch;
    }

    private void SkipTrivia()
    {
        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Current))
            {
                Advance();
                continue;
            }

            if (Current == '/' && Peek() == '/')
            {
                while (!IsAtEnd && Current != '\n')
                    Advance();
                continue;
            }

            if (Current == '(' && Peek() == '*')
            {
                var start = CurrentPosition;
                Advance();
                Advance();
                bool closed = false;
                while (!IsAtEnd)
                {
                    if (Current == '*' && Peek() == ')')
                    {
                        Advance();
                        Advance();
                        closed = true;
                        break;
                    }
                    Advance();
                }

                if (!closed)
                {
                    _diagnostics.Add(new Diagnostic(
                        "LEX001",
                        "Незакритий багаторядковий коментар '(* ... *)'.",
                        new SourceSpan(start, CurrentPosition)));
                }
                continue;
            }

            if (Current == '/' && Peek() == '*')
            {
                var start = CurrentPosition;
                Advance();
                Advance();
                bool closed = false;
                while (!IsAtEnd)
                {
                    if (Current == '*' && Peek() == '/')
                    {
                        Advance();
                        Advance();
                        closed = true;
                        break;
                    }
                    Advance();
                }

                if (!closed)
                {
                    _diagnostics.Add(new Diagnostic(
                        "LEX002",
                        "Незакритий багаторядковий коментар '/* ... */'.",
                        new SourceSpan(start, CurrentPosition)));
                }
                continue;
            }

            break;
        }
    }

    private Token ReadToken()
    {
        var start = CurrentPosition;
        char ch = Current;

        if (char.IsLetter(ch))
            return ReadIdentifierOrKeyword(start);

        if (char.IsDigit(ch))
            return ReadNumber(start);

        Advance();

        switch (ch)
        {
            case ';':
                return new Token(TokenKind.Semicolon, ";", new SourceSpan(start, CurrentPosition));
            case '(':
                return new Token(TokenKind.LParen, "(", new SourceSpan(start, CurrentPosition));
            case ')':
                return new Token(TokenKind.RParen, ")", new SourceSpan(start, CurrentPosition));
            case '+':
                return new Token(TokenKind.Plus, "+", new SourceSpan(start, CurrentPosition));
            case '-':
                return new Token(TokenKind.Minus, "-", new SourceSpan(start, CurrentPosition));
            case '*':
                return new Token(TokenKind.Star, "*", new SourceSpan(start, CurrentPosition));
            case '/':
                return new Token(TokenKind.Slash, "/", new SourceSpan(start, CurrentPosition));
            case '%':
                return new Token(TokenKind.Percent, "%", new SourceSpan(start, CurrentPosition));
            case '=':
                return new Token(TokenKind.Equal, "=", new SourceSpan(start, CurrentPosition));
            case ':':
                if (Current == '=')
                {
                    Advance();
                    return new Token(TokenKind.Assign, ":=", new SourceSpan(start, CurrentPosition));
                }
                var badColonSpan = new SourceSpan(start, CurrentPosition);
                _diagnostics.Add(new Diagnostic("LEX003", "Очікувалося ':=' після ':'.", badColonSpan));
                return new Token(TokenKind.BadToken, ":", badColonSpan);

            case '<':
                if (Current == '=')
                {
                    Advance();
                    return new Token(TokenKind.LessEqual, "<=", new SourceSpan(start, CurrentPosition));
                }
                if (Current == '>')
                {
                    Advance();
                    return new Token(TokenKind.NotEqual, "<>", new SourceSpan(start, CurrentPosition));
                }
                return new Token(TokenKind.Less, "<", new SourceSpan(start, CurrentPosition));

            case '>':
                if (Current == '=')
                {
                    Advance();
                    return new Token(TokenKind.GreaterEqual, ">=", new SourceSpan(start, CurrentPosition));
                }
                return new Token(TokenKind.Greater, ">", new SourceSpan(start, CurrentPosition));

            case '!':
                if (Current == '=')
                {
                    Advance();
                    return new Token(TokenKind.NotEqual, "!=", new SourceSpan(start, CurrentPosition));
                }
                var badBangSpan = new SourceSpan(start, CurrentPosition);
                _diagnostics.Add(new Diagnostic("LEX004", "Неочікуваний символ '!'. Для заперечення використовуйте 'not', для нерівності — '<>' або '!='.", badBangSpan));
                return new Token(TokenKind.BadToken, "!", badBangSpan);

            default:
                var badSpan = new SourceSpan(start, CurrentPosition);
                _diagnostics.Add(new Diagnostic("LEX005", $"Невідомий символ '{ch}'.", badSpan));
                return new Token(TokenKind.BadToken, ch.ToString(), badSpan);
        }
    }

    private Token ReadIdentifierOrKeyword(SourcePosition start)
    {
        int startOffset = _offset;
        while (!IsAtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
            Advance();

        string lexeme = _source[startOffset.._offset];
        var span = new SourceSpan(start, CurrentPosition);

        var kind = Keywords.GetValueOrDefault(lexeme, TokenKind.Identifier);
        return new Token(kind, lexeme, span);
    }

    private Token ReadNumber(SourcePosition start)
    {
        int startOffset = _offset;
        while (!IsAtEnd && char.IsDigit(Current))
            Advance();

        string lexeme = _source[startOffset.._offset];
        var span = new SourceSpan(start, CurrentPosition);

        if (!int.TryParse(lexeme, out int value))
        {
            _diagnostics.Add(new Diagnostic(
                "LEX006",
                $"Ціле число '{lexeme}' виходить за межі діапазону Int32.",
                span));
            return new Token(TokenKind.BadToken, lexeme, span);
        }

        return new Token(TokenKind.Number, lexeme, span, value);
    }
}