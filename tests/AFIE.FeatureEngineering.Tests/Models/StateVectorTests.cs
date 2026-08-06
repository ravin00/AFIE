using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Models;

public class StateVectorValidationTests
{
    private static StateVector Valid() =>
        new("nginx", "default", DateTimeOffset.UtcNow, new float[StateVector.Dimensions]);

    [Fact]
    public void With_WrongLength_Throws()
    {
        var v = Valid();
        Assert.Throws<ArgumentException>(() => v with { Values = new float[10] });
    }

    [Fact]
    public void With_NullValues_Throws()
    {
        var v = Valid();
        Assert.Throws<ArgumentNullException>(() => v with { Values = null! });
    }

    [Fact]
    public void ObjectInitializer_WrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StateVector("nginx", "default", DateTimeOffset.UtcNow, new float[StateVector.Dimensions])
            {
                Values = new float[46]
            });
    }
}