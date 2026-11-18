using Sipl.Core.Diagnostics;

namespace Sipl.Core.Syntax;

public enum TokenKind
{
    EndOfFile,
    BadToken,

    Identifier,
    Number,

    Skip,
    Begin,
    End,
    If,
    Then,
    Else,
    While,
    Do,
    True,
    False,
    And,
    Or,
    Not,

    Assign,         // :=
    Semicolon,      // ;
    LParen,         // (
    RParen,         // )
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    Percent,        // %
    Equal,          // =
    NotEqual,       // <> або !=
    Less,           // <
    LessEqual,      // <=
    Greater,        // >
    GreaterEqual    // >=
}

public sealed record Token(
    TokenKind Kind,
    string Lexeme,
    SourceSpan Span,
    int? IntValue = null);