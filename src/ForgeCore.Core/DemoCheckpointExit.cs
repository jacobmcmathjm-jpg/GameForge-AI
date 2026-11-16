#nullable disable
using System;
namespace ForgeCore.Demo
{
    public enum DemoCheckpointExitState { Idle,Waiting,Blocked,Ready }
    // Poll from the UI thread; never blocks it. Forced exit is an explicit choice after a visible block.
    public sealed class DemoCheckpointExit
    {
        public const double WaitSeconds=2;
        public DemoCheckpointExitState State {get;private set;}
        private double deadline,last;
        public void Request(bool pending,bool failed,double now){
            Time(now);if(State!=DemoCheckpointExitState.Idle)return;
            deadline=now+WaitSeconds;Set(pending,failed,now);
        }
        public void Poll(bool pending,bool failed,double now){
            Time(now);if(State==DemoCheckpointExitState.Idle||State==DemoCheckpointExitState.Ready)return;
            Set(pending,failed,now);
        }
        public void WaitAgain(double now){
            Time(now);if(State!=DemoCheckpointExitState.Blocked)throw new InvalidOperationException("Exit is not blocked.");
            deadline=now+WaitSeconds;State=DemoCheckpointExitState.Waiting;
        }
        public void Cancel(){State=DemoCheckpointExitState.Idle;deadline=last=0;}
        public void ConfirmWithoutLatest(){
            if(State!=DemoCheckpointExitState.Blocked)throw new InvalidOperationException("Explicit blocked-exit choice required.");
            State=DemoCheckpointExitState.Ready;
        }
        private void Set(bool pending,bool failed,double now)=>State=failed?DemoCheckpointExitState.Blocked:!pending?DemoCheckpointExitState.Ready:now>=deadline?DemoCheckpointExitState.Blocked:DemoCheckpointExitState.Waiting;
        private void Time(double now){if(double.IsNaN(now)||double.IsInfinity(now)||now<0||now<last)throw new ArgumentOutOfRangeException(nameof(now));last=now;}
    }
}
