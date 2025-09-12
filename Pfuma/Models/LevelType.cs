using System.ComponentModel;

namespace Pfuma.Models;

public enum LevelType
{
    [Description("Premium")]
    Premium,
    [Description("Discount")]
    Discount,
    [Description("Equilibrium")]
    Equilibrium,
    [Description("Orderflow")]
    Orderflow,
    [Description("FVG")]
    FairValueGap,
    [Description("Breaker Block")]
    BreakerBlock,
    [Description("Unicorn")]
    Unicorn,
    [Description("CISD")]
    CISD,
    [Description("Rejection Block")]
    RejectionBlock,
    [Description("Order Block")]
    OrderBlock,
    [Description("Propulsion Block")]
    PropulsionBlock,
}