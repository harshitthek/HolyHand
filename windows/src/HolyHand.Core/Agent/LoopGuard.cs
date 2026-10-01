using System.Security.Cryptography;
using System.Text;
using HolyHand.Core.Models;

namespace HolyHand.Core.Agent;

public class LoopGuard
{
    private readonly int _maxConsecutiveStalls;
    private string? _lastStateSignature;
    private int _consecutiveStalls;

    public int ConsecutiveStalls => _consecutiveStalls;
    public bool IsStalled => _consecutiveStalls >= _maxConsecutiveStalls;

    public LoopGuard(int maxConsecutiveStalls = 10)
    {
        _maxConsecutiveStalls = maxConsecutiveStalls;
    }

    public bool RecordObservation(IReadOnlyList<AccessibilityElement> elements)
    {
        var currentSignature = ComputeSignature(elements);

        if (_lastStateSignature != null && currentSignature == _lastStateSignature)
        {
            _consecutiveStalls++;
        }
        else
        {
            _consecutiveStalls = 1;
        }

        _lastStateSignature = currentSignature;
        return IsStalled;
    }

    public void Reset()
    {
        _lastStateSignature = null;
        _consecutiveStalls = 0;
    }

    private static string ComputeSignature(IReadOnlyList<AccessibilityElement> elements)
    {
        var sb = new StringBuilder();
        foreach (var el in elements)
        {
            sb.Append(el.Id).Append('|')
              .Append(el.Role).Append('|')
              .Append(el.DisplayLabel).Append('|')
              .Append(el.Value).Append('|')
              .Append(el.Focused).Append('|')
              .Append(el.Enabled).Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
