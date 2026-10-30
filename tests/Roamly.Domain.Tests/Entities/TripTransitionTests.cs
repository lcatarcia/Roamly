using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.Domain.Tests.Entities;

public sealed class TripTransitionTests
{
    [Theory]
    [InlineData(TripStatus.Planned, TripStatus.Planned, false)]
    [InlineData(TripStatus.Planned, TripStatus.Active, true)]
    [InlineData(TripStatus.Planned, TripStatus.Completed, false)]
    [InlineData(TripStatus.Planned, TripStatus.Cancelled, true)]
    [InlineData(TripStatus.Active, TripStatus.Planned, false)]
    [InlineData(TripStatus.Active, TripStatus.Active, false)]
    [InlineData(TripStatus.Active, TripStatus.Completed, true)]
    [InlineData(TripStatus.Active, TripStatus.Cancelled, true)]
    [InlineData(TripStatus.Completed, TripStatus.Planned, false)]
    [InlineData(TripStatus.Completed, TripStatus.Active, true)]
    [InlineData(TripStatus.Completed, TripStatus.Completed, false)]
    [InlineData(TripStatus.Completed, TripStatus.Cancelled, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.Planned, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.Active, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.Completed, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.Cancelled, false)]
    public void TransitionTo_obeys_the_documented_matrix(
        TripStatus from,
        TripStatus to,
        bool isLegal)
    {
        var trip = CreateTripInState(from);

        if (isLegal)
        {
            trip.TransitionTo(to);
            Assert.Equal(to, trip.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => trip.TransitionTo(to));
            Assert.Equal(from, trip.Status);
        }
    }

    private static Trip CreateTripInState(TripStatus status)
    {
        var trip = new Trip();
        switch (status)
        {
            case TripStatus.Planned:
                break;
            case TripStatus.Active:
                trip.TransitionTo(TripStatus.Active);
                break;
            case TripStatus.Completed:
                trip.TransitionTo(TripStatus.Active);
                trip.TransitionTo(TripStatus.Completed);
                break;
            case TripStatus.Cancelled:
                trip.TransitionTo(TripStatus.Cancelled);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return trip;
    }
}
