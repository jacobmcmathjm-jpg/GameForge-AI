using System;

// This exact file is also embedded in the Unity kit. Keep it engine-independent and
// compatible with Unity's runtime, so host tests exercise the shipped curve math.
namespace ForgeCore.Motion
{
    public enum GuardMotionClip { Idle, Walk, Run, Aim, Fire, Reload, Hit, Death }

    public struct GuardMotionSample
    {
        public float PelvisSide, PelvisHeight, PelvisForward, PelvisYaw, PelvisPitch, PelvisRoll;
        public float ChestPitch, ChestYaw, ChestRoll, HeadPitch, HeadYaw;
        public float Recoil, ReloadReach, KneeFold;
    }

    public static class GuardMotionProfile
    {
        public const string Recipe = "generic_mannequin_motion_v3";
        public static float Ease(float value)
        {
            RequireFinite(value);
            var t = Clamp(value);
            return Clamp(t * t * t * (t * (t * 6 - 15) + 10));
        }
        public static float SwingLift(float value)
        {
            RequireFinite(value);
            var sine = (float)Math.Sin(Math.PI * Clamp(value));
            return sine * sine; // Zero lift velocity at take-off and touchdown.
        }
        public static float ReloadContact(float seconds)
        {
            RequireFinite(seconds);
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            var t = seconds / 1.6f;
            return Ease(t / .18f) * (1 - Ease((t - .80f) / .20f));
        }
        public static GuardMotionSample Sample(GuardMotionClip clip, float t)
        {
            RequireFinite(t);
            if (t < 0 || t > 1 || (int)clip < 0 || (int)clip > 7)
                throw new ArgumentOutOfRangeException(nameof(t));
            var p = new GuardMotionSample();
            var cycle = t * Math.PI * 2;
            var sine = (float)Math.Sin(cycle);
            var cosine = (float)Math.Cos(cycle);
            if (clip == GuardMotionClip.Walk || clip == GuardMotionClip.Run)
            {
                var run = clip == GuardMotionClip.Run;
                p.PelvisSide = (run ? .014f : .020f) * sine;
                p.PelvisHeight = -(run ? .17f : .12f) + .010f * (float)Math.Cos(cycle * 2);
                p.PelvisYaw = (run ? 4 : 3) * sine;
                p.PelvisRoll = -2 * sine;
                p.ChestPitch = run ? 7 : 3;
                p.ChestYaw = -p.PelvisYaw * .65f;
                p.ChestRoll = sine;
                p.HeadYaw = -p.PelvisYaw * .35f;
            }
            else if (clip == GuardMotionClip.Idle || clip == GuardMotionClip.Aim)
            {
                var aim = clip == GuardMotionClip.Aim;
                p.PelvisHeight = .0025f * sine;
                p.ChestPitch = (aim ? 2 : 0) + (aim ? .3f : .7f) * sine;
                p.ChestRoll = .25f * cosine;
                p.HeadPitch = -.25f * sine;
            }
            else if (clip == GuardMotionClip.Fire)
            {
                // Impact peak is aligned with the existing .08s shot marker, not clip midpoint.
                var attack = Ease(t / .32f);
                p.Recoil = attack * (1 - Ease((t - .32f) / .68f));
                p.ChestPitch = -3.5f * p.Recoil;
                p.ChestYaw = 1.5f * p.Recoil;
                p.HeadPitch = 1.2f * p.Recoil;
            }
            else if (clip == GuardMotionClip.Reload)
            {
                p.ReloadReach = ReloadContact(t * 1.6f);
                p.ChestPitch = 3 * p.ReloadReach;
                p.ChestYaw = -4 * p.ReloadReach;
                p.HeadPitch = 8 * p.ReloadReach;
                p.HeadYaw = -3 * p.ReloadReach;
            }
            else if (clip == GuardMotionClip.Hit)
            {
                var envelope = Ease(t / .18f) * (1 - Ease((t - .18f) / .82f));
                p.ChestPitch = -8 * envelope;
                p.ChestRoll = 3 * envelope;
                p.PelvisPitch = 3 * envelope;
                p.HeadPitch = 5 * envelope;
            }
            else
            {
                // Reaction -> knees give way -> accelerated fall -> asymmetrical settle.
                // All living actions above intentionally retain the accepted v2 curve math.
                var buckle = Ease(t / .28f);
                var fall = Ease((t - .16f) / .58f);
                var settle = Ease((t - .72f) / .28f);
                p.PelvisHeight = -.12f * buckle - .60f * fall;
                p.PelvisForward = .04f * buckle + .24f * fall;
                p.PelvisPitch = -4 * buckle * (1 - fall) + 78 * fall;
                p.PelvisRoll = -5 * fall + 14 * settle;
                p.PelvisYaw = -7 * fall + settle;
                p.ChestPitch = -6 * buckle * (1 - fall) + 10 * fall - 7 * settle;
                p.HeadPitch = -4 * buckle * (1 - fall) + 14 * fall - 8 * settle;
                p.KneeFold = 36 * buckle * (1 - .65f * fall) + 6 * settle;
            }
            return p;
        }
        private static float Clamp(float value) { return Math.Max(0, Math.Min(1, value)); }
        private static void RequireFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Motion time must be finite.");
        }
    }
}
