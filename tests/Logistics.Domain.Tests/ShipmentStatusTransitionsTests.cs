using Logistics.Domain.Shipments;

namespace Logistics.Domain.Tests;

public class ShipmentStatusTransitionsTests
{
    public static TheoryData<ShipmentStatus> AllStatuses()
    {
        var data = new TheoryData<ShipmentStatus>();
        foreach (var status in Enum.GetValues<ShipmentStatus>())
            data.Add(status);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_has_a_rule(ShipmentStatus status)
    {
        // Throws KeyNotFoundException if someone adds an enum value but forgets the table.
        _ = ShipmentStatusTransitions.NextFrom(status);
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void Delivered_and_cancelled_are_final(ShipmentStatus status)
    {
        Assert.True(ShipmentStatusTransitions.IsFinal(status));
        Assert.Empty(ShipmentStatusTransitions.NextFrom(status));
    }

    [Fact]
    public void Every_non_final_status_can_eventually_reach_delivered()
    {
        foreach (var start in Enum.GetValues<ShipmentStatus>().Where(s => s != ShipmentStatus.Cancelled))
        {
            var seen = new HashSet<ShipmentStatus>();
            var queue = new Queue<ShipmentStatus>([start]);
            while (queue.TryDequeue(out var current))
            {
                if (!seen.Add(current)) continue;
                foreach (var next in ShipmentStatusTransitions.NextFrom(current))
                    queue.Enqueue(next);
            }

            Assert.Contains(ShipmentStatus.Delivered, seen);
        }
    }
}
