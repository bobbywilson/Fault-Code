using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FaultCode.Domain;

public sealed record DocumentPage(int Number, string Text);
public sealed record DocumentContent(string Title, string SourceVersion, string System, string Model, string SoftwareVersion,
    string Access, ImmutableArray<DocumentPage> Pages);
public sealed record DocumentRevision(long Version, DocumentContent Content, string State, string ActorId, DateTimeOffset At);
public sealed record DocumentPublication(long Version, DocumentContent Content, string Checksum, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record TechnicalDocument(Guid Id, string TenantId, string SourceId, long Version, bool Deleted, DocumentContent Draft,
    ImmutableArray<DocumentRevision> History, ImmutableArray<DocumentPublication> Publications, string? IndexedProfile);
public sealed record DocumentChunk(Guid Id, int Page, int Offset, string Text, string Checksum);
public static class DocumentRules
{
    public const string Algorithm = "chars-v1";
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static Guid Id(string text) => new(Convert.FromHexString(Hash(text)[..32]));
    public static bool Source(string? value) => value is not null && value.Length <= 100
        && Regex.IsMatch(value, "^SYN-[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant);
    public static void Validate(DocumentContent c)
    {
        if (!FaultRules.Text(c.Title, 200) || !FaultRules.Software(c.SourceVersion)
            || c.System is not ("EPS" or "steer-by-wire" or "steering-ADAS") || !FaultRules.DemoModel(c.Model)
            || !FaultRules.Software(c.SoftwareVersion) || c.Access is not ("tenant" or "publisher")
            || c.Pages.IsDefault || c.Pages.Length is < 1 or > 20 || c.Pages.Any(p => p is null)
            || c.Pages.Select(p => p.Number).Distinct().Count() != c.Pages.Length
            || c.Pages.Sum(p => (long)(p.Text?.Length ?? 0)) > 40000) throw new ArgumentException("Invalid document");
        foreach (var page in c.Pages)
        {
            if (page.Number is < 1 or > 10000 || string.IsNullOrWhiteSpace(page.Text) || page.Text.Length > 10000
                || page.Text.Any(x => char.IsControl(x) && x is not ('\n' or '\r' or '\t'))
                || !ValidUnicode(page.Text)) throw new ArgumentException("Invalid page");
        }
    }
    private static bool ValidUnicode(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }
    public static DocumentContent Canonical(DocumentContent c) => c with { Pages = [.. c.Pages.OrderBy(p => p.Number)] };
    public static string Checksum(DocumentContent c) => Hash(JsonSerializer.Serialize(Canonical(c)));
    public static string RevisionKey(TechnicalDocument d, DocumentPublication p) => $"{d.Id:N}:{p.Version}:{p.Checksum}";
    public static ImmutableArray<DocumentChunk> Chunks(TechnicalDocument d, DocumentPublication p)
    {
        var chunks = ImmutableArray.CreateBuilder<DocumentChunk>();
        foreach (var page in p.Content.Pages.OrderBy(p => p.Number))
            for (var offset = 0; offset < page.Text.Length;)
            {
                var length = Math.Min(500, page.Text.Length - offset);
                if (char.IsHighSurrogate(page.Text[offset + length - 1])) length--;
                var text = page.Text.Substring(offset, length); var checksum = Hash(text);
                var id = Id($"{d.TenantId}:{RevisionKey(d, p)}:{Algorithm}:{page.Number}:{offset}:{checksum}");
                chunks.Add(new(id, page.Number, offset, text, checksum)); offset += length;
            }
        return chunks.ToImmutable();
    }
}
