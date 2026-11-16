using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoCheckpointExitTests
{
    [Fact]public void CompletedQueueAllowsExitImmediately(){var e=new DemoCheckpointExit();e.Request(false,false,0);Assert.Equal(DemoCheckpointExitState.Ready,e.State);}
    [Fact]public void PendingExitPollsWithoutBlockingAndBecomesReadyOnlyAfterDrain(){
        var e=new DemoCheckpointExit();e.Request(true,false,10);Assert.Equal(DemoCheckpointExitState.Waiting,e.State);
        e.Poll(true,false,11.999);Assert.Equal(DemoCheckpointExitState.Waiting,e.State);
        e.Poll(false,false,12);Assert.Equal(DemoCheckpointExitState.Ready,e.State);
    }
    [Fact]public void TimeoutDoesNotPretendSaveCompletedOrAutomaticallyForceQuit(){
        var e=new DemoCheckpointExit();e.Request(true,false,0);e.Poll(true,false,2);Assert.Equal(DemoCheckpointExitState.Blocked,e.State);
        e.Poll(true,false,100);Assert.Equal(DemoCheckpointExitState.Blocked,e.State);
        e.ConfirmWithoutLatest();Assert.Equal(DemoCheckpointExitState.Ready,e.State);
    }
    [Fact]public void FailureRequiresExplicitChoiceEvenWhenNoWriteRemains(){
        var e=new DemoCheckpointExit();e.Request(false,true,0);Assert.Equal(DemoCheckpointExitState.Blocked,e.State);
        e.Poll(false,true,1);Assert.Equal(DemoCheckpointExitState.Blocked,e.State);e.Cancel();Assert.Equal(DemoCheckpointExitState.Idle,e.State);
    }
    [Fact]public void WaitingAgainAndCancellationPreserveTheQueueDecision(){
        var e=new DemoCheckpointExit();e.Request(true,false,0);e.Poll(true,false,2);e.WaitAgain(3);e.Poll(true,false,4.9);
        Assert.Equal(DemoCheckpointExitState.Waiting,e.State);e.Cancel();e.Request(false,false,0);Assert.Equal(DemoCheckpointExitState.Ready,e.State);
    }
    [Fact]public void EarlyForceAndWaitAreRejected(){var e=new DemoCheckpointExit();Assert.Throws<InvalidOperationException>(e.ConfirmWithoutLatest);Assert.Throws<InvalidOperationException>(()=>e.WaitAgain(0));}
    [Theory][InlineData(-1)][InlineData(double.NaN)][InlineData(double.PositiveInfinity)]public void InvalidClockIsRejected(double t)=>Assert.Throws<ArgumentOutOfRangeException>(()=>new DemoCheckpointExit().Request(true,false,t));
    [Fact]public void ReversedClockIsRejected(){var e=new DemoCheckpointExit();e.Request(true,false,2);Assert.Throws<ArgumentOutOfRangeException>(()=>e.Poll(true,false,1));}
}
