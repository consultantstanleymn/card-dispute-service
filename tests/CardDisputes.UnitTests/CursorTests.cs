using CardDisputes.Infrastructure;

namespace CardDisputes.UnitTests;

#pragma warning disable CA1707

public sealed class CursorTests
{
    private static readonly DateTimeOffset OpenedAt =
        new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero).AddTicks(1234560);

    [Fact]
    public void Cursor_Round_Trips()
    {
        var id = Guid.NewGuid();

        var cursor = Cursor.Encode(new CursorPosition(OpenedAt, id));
        var decoded = Cursor.Decode(cursor);

        Assert.Equal(OpenedAt, decoded.OpenedAt);
        Assert.Equal(id, decoded.Id);
    }

    [Fact]
    public void Tampered_Cursor_Is_Rejected()
    {
        var cursor = Cursor.Encode(new CursorPosition(OpenedAt, Guid.NewGuid()));

        var characters = cursor.ToCharArray();
        var index = characters.Length / 2;
        characters[index] = characters[index] == 'A' ? 'B' : 'A';
        var tampered = new string(characters);

        Assert.Throws<InvalidCursorException>(() => Cursor.Decode(tampered));
    }

    [Fact]
    public void Cursor_From_Another_Sort_Is_Rejected()
    {
        var cursor = Cursor.Encode(new CursorPosition(OpenedAt, Guid.NewGuid()), Cursor.SortOpenedAtId);

        Assert.Throws<InvalidCursorException>(
            () => Cursor.Decode(cursor, "opened_at_asc,id_asc"));
    }
}
