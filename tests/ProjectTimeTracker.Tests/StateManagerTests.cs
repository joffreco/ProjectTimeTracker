using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ProjectTimeTracker.Domain;
using ProjectTimeTracker.Services;

namespace ProjectTimeTracker.Tests;

public class StateManagerTests
{
    private readonly IFirestoreService _firestoreService;
    private readonly ILogger<StateManager> _logger;
    private readonly StateManager _sut;

    public StateManagerTests()
    {
        _firestoreService = Substitute.For<IFirestoreService>();
        _logger = Substitute.For<ILogger<StateManager>>();
        _sut = new StateManager(_firestoreService, _logger);
    }

    [Fact]
    public void CurrentState_DefaultsToNone()
    {
        Assert.Equal(State.None, _sut.CurrentState);
    }

    [Fact]
    public async Task InitializeAsync_RebuildStateFromLatestEvent()
    {
        // Arrange — Firestore returns one recent event.
        List<StateEvent> events = new List<StateEvent>
        {
            StateEvent.Create(State.None, State.Project1)
        };
        _firestoreService.GetRecentEventsAsync(1).Returns(events);
        _firestoreService.StartListening(Arg.Any<Action<QuerySnapshot>>())
            .Returns((FirestoreChangeListener)null!);

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.Equal(State.Project1, _sut.CurrentState);
    }

    [Fact]
    public async Task InitializeAsync_NoEvents_StartsAsNone()
    {
        _firestoreService.GetRecentEventsAsync(1).Returns(new List<StateEvent>());
        _firestoreService.StartListening(Arg.Any<Action<QuerySnapshot>>())
            .Returns((FirestoreChangeListener)null!);

        await _sut.InitializeAsync();

        Assert.Equal(State.None, _sut.CurrentState);
    }

    [Fact]
    public async Task ChangeStateAsync_IgnoresDuplicateTransition()
    {
        // CurrentState defaults to None — transitioning to None should be a no-op.
        await _sut.ChangeStateAsync(State.None);

        await _firestoreService.DidNotReceive().SendEventAsync(Arg.Any<StateEvent>());
    }

    [Fact]
    public async Task ChangeStateAsync_RejectsProjectToProjectWithoutNone()
    {
        // Arrange — move to Project1 first.
        List<StateEvent> events = new List<StateEvent>
        {
            StateEvent.Create(State.None, State.Project1)
        };
        _firestoreService.GetRecentEventsAsync(1).Returns(events);
        _firestoreService.StartListening(Arg.Any<Action<QuerySnapshot>>())
            .Returns((FirestoreChangeListener)null!);
        await _sut.InitializeAsync();

        // Act — try Project1 → Project2 (invalid without going through None).
        await _sut.ChangeStateAsync(State.Project2);

        // Assert — no event should have been sent for the invalid transition.
        await _firestoreService.DidNotReceive().SendEventAsync(Arg.Any<StateEvent>());
    }

    [Fact]
    public async Task ChangeStateAsync_ValidTransition_SendsEvent()
    {
        // None → Project1 is valid.
        await _sut.ChangeStateAsync(State.Project1);

        await _firestoreService.Received(1).SendEventAsync(
            Arg.Is<StateEvent>(e => e.FromState == "None" && e.ToState == "Project1"));
    }

    [Fact]
    public async Task StateChanged_FiresOnStateChange()
    {
        // Arrange
        List<StateEvent> events = new List<StateEvent>
        {
            StateEvent.Create(State.None, State.Project1)
        };
        _firestoreService.GetRecentEventsAsync(1).Returns(events);
        _firestoreService.StartListening(Arg.Any<Action<QuerySnapshot>>())
            .Returns((FirestoreChangeListener)null!);

        State? received = null;
        _sut.StateChanged += s => received = s;

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.Equal(State.Project1, received);
    }
}

