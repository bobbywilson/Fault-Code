using FaultCode.Domain;
namespace FaultCode.Domain.Tests;

public sealed class DocumentRulesTests
{
    private static DocumentContent Content(string text) => new("Synthetic EPS", "1", "EPS", "SYN-MODEL-1", "1.0", "tenant", [new(7, text)]);
    [Fact]
    public void Page_chunks_are_deterministic_lossless_versioned_and_unicode_safe()
    {
        var c = Content(new string('a', 499) + "😀" + new string('b', 510)); DocumentRules.Validate(c);
        var d = new TechnicalDocument(Guid.NewGuid(), "tenant", "SYN-DOC", 2, false, c, [], [], null);
        var p = new DocumentPublication(2, c, DocumentRules.Checksum(c), "actor", DateTimeOffset.UnixEpoch);
        var chunks = DocumentRules.Chunks(d, p); Assert.Equal(3, chunks.Length);
        Assert.Equal(c.Pages[0].Text, string.Concat(chunks.Select(x => x.Text))); Assert.All(chunks, x => Assert.Equal(7, x.Page));
        Assert.Equal(chunks.ToArray(), DocumentRules.Chunks(d, p).ToArray());
        Assert.NotEqual(chunks[0].Id, DocumentRules.Chunks(d with { TenantId = "other" }, p)[0].Id);
        Assert.NotEqual(chunks[0].Id, DocumentRules.Chunks(d, p with { Version = 4 })[0].Id);
        Assert.Equal(499, chunks[1].Offset); Assert.Equal(DocumentRules.Hash(chunks[1].Text), chunks[1].Checksum);
    }
    [Fact]
    public void Invalid_page_text_is_rejected()
    {
        foreach (var text in new[] { "", "\0bad", new string('\ud800', 1), new string('\udc00', 1) })
            Assert.Throws<ArgumentException>(() => DocumentRules.Validate(Content(text)));
    }
    [Fact]
    public void Bounds_duplicates_access_applicability_and_system_are_validated()
    {
        var c = Content("Synthetic");
        foreach (var invalid in new[] { c with { Pages = [] }, c with { Pages = [new(1, "a"), new(1, "b")] },
            c with { Pages = [new(0, "a")] }, c with { Pages = [new(1, new('a', 10001))] }, c with { Access = "admin" },
            c with { Model = "real-model" }, c with { SoftwareVersion = " " }, c with { System = "brakes" }, c with { SourceVersion = "../x" },
            c with { Pages = [.. Enumerable.Range(1, 5).Select(i => new DocumentPage(i, new('a', 10000)))] } })
            Assert.Throws<ArgumentException>(() => DocumentRules.Validate(invalid));
        foreach (var system in new[] { "EPS", "steer-by-wire", "steering-ADAS" }) DocumentRules.Validate(c with { System = system });
        Assert.False(DocumentRules.Source("SYN-../a")); Assert.True(DocumentRules.Source("SYN-DOC_1"));
        var sorted = c with { Pages = [new(2, "b"), new(1, "a")] };
        Assert.Equal(DocumentRules.Checksum(sorted), DocumentRules.Checksum(sorted with { Pages = [.. sorted.Pages.Reverse()] }));
    }
}
