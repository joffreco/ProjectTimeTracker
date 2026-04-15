using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Tests;

public class StateEventTests
{
    [Fact]
    public void Create_SetsFromAndToState()
    {
        StateEvent evt = StateEvent.Create(State.None, State.Project1);

        Assert.Equal("None", evt.FromState);
        Assert.Equal("Project1", evt.ToState);
    }

    [Fact]
    public void Create_SetsUtcTimestamp()
    {
        DateTime before = DateTime.UtcNow;
        StateEvent evt = StateEvent.Create(State.None, State.Project1);
        DateTime after = DateTime.UtcNow;

        DateTime eventTime = evt.Timestamp.ToDateTime();
        Assert.True(eventTime >= before && eventTime <= after);
    }

    [Fact]
    public void GetToState_ParsesEnumCorrectly()
    {
        StateEvent evt = StateEvent.Create(State.None, State.Project2);

        Assert.Equal(State.Project2, evt.GetToState());
    }

    [Fact]
    public void GetFromState_ParsesEnumCorrectly()
    {
        StateEvent evt = StateEvent.Create(State.Project1, State.None);

        Assert.Equal(State.Project1, evt.GetFromState());
    }

    [Fact]
    public void GetToState_ReturnsNone_ForInvalidValue()
    {
        StateEvent evt = new StateEvent { FromState = "None", ToState = "InvalidState" };

        Assert.Equal(State.None, evt.GetToState());
    }

    [Fact]
    public void GetFromState_ReturnsNone_ForInvalidValue()
    {
        StateEvent evt = new StateEvent { FromState = "Garbage", ToState = "None" };

        Assert.Equal(State.None, evt.GetFromState());
    }
}

