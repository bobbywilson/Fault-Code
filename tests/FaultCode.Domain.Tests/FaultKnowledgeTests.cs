using FaultCode.Domain;
namespace FaultCode.Domain.Tests;

public sealed class FaultKnowledgeTests
{
    [Theory]
    [InlineData(" syn-eps:a01 ", "EPS", "SYN-EPS:A01")]
    [InlineData("syn-sbw:001", "steer-by-wire", "SYN-SBW:001")]
    [InlineData("syn-adas:001", "steering-ADAS", "SYN-ADAS:001")]
    public void Codes_have_stable_synthetic_namespaces(string code, string system, string expected) => Assert.Equal(expected, FaultRules.Code(code, system));
    [Theory]
    [InlineData("SYN-EPS:001", "steer-by-wire")]
    [InlineData("P0123", "EPS")]
    [InlineData("SYN-EPS:", "EPS")]
    [InlineData("SYN-EPS:A/1", "EPS")]
    [InlineData("SYN-EPS:é", "EPS")]
    [InlineData("SYN-EPS:001", "brakes")]
    public void Invalid_or_mismatched_codes_reject(string code, string system) => Assert.Throws<ArgumentException>(() => FaultRules.Code(code, system));
}
