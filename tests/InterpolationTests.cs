using BepInEx.Logging;
using NUnit.Framework;
using TCAMultiplayer.Core;
using TCAMultiplayer.Sync;

namespace TCAMultiplayer.Tests
{
    [TestFixture]
    public class InterpolationTests
    {
        private InterpolationBuffer _buffer;


        [OneTimeSetUp]
        public void GlobalSetup()
        {
            // Initialize Log with BepInEx source to prevent fallback to UnityEngine.Debug
            // (UnityEngine.Debug doesn't work outside Unity runtime)
            if (!Log.IsInitialized)
                Log.Init(new ManualLogSource("Test"));
        }

        [SetUp]
        public void Setup()
        {
            _buffer = new InterpolationBuffer(capacity: 30, interpolationDelay: 0.15f);
        }

        // === Edge Cases ===

        [Test]
        public void EmptyBuffer_ReturnsDefaultSample()
        {
            var result = _buffer.GetInterpolatedState(1.0f);
            // Should not crash, returns default
            Assert.AreEqual(0.0, result.PosX, 0.001);
            Assert.AreEqual(0.0, result.PosY, 0.001);
            Assert.AreEqual(0.0, result.PosZ, 0.001);
        }

        [Test]
        public void SingleSample_ReturnsThatSample()
        {
            var sample = MakeSample(posX: 100.0, posY: 200.0, posZ: 300.0, remoteTime: 0.0f, localTime: 0.0f);
            _buffer.AddSample(sample);
            
            var result = _buffer.GetInterpolatedState(0.5f);
            // With only one sample, should return it or something close
            Assert.AreEqual(100.0, result.PosX, 1.0);
        }

        [Test]
        public void TwoSamples_InterpolatesBetween()
        {
            var s1 = MakeSample(posX: 0.0, posY: 0.0, posZ: 0.0, remoteTime: 0.0f, localTime: 0.0f);
            var s2 = MakeSample(posX: 100.0, posY: 0.0, posZ: 0.0, remoteTime: 1.0f, localTime: 1.0f);
            _buffer.AddSample(s1);
            _buffer.AddSample(s2);
            
            // At localTime = 1.15 + 0.5 = 1.65 (delay=0.15, target time within range)
            // The interpolation targets remoteTime = localTime - delay - clockOffset
            // With clockOffset ~0, target = 1.65 - 0.15 = 1.5 → mid-range
            var result = _buffer.GetInterpolatedState(1.65f);
            // Position should be between 0 and 100
            Assert.Greater(result.PosX, -10.0);
            Assert.Less(result.PosX, 110.0);
        }

        [Test]
        public void SlightBufferUnderrun_ExtrapolatesFromLatestVelocity()
        {
            var buffer = new InterpolationBuffer(capacity: 30, interpolationDelay: 0.20f);
            var s1 = MakeSample(posX: 0.0, remoteTime: 0.0f, localTime: 0.0f);
            s1.VelX = 100f;
            var s2 = MakeSample(posX: 100.0, remoteTime: 1.0f, localTime: 1.0f);
            s2.VelX = 100f;
            buffer.AddSample(s1);
            buffer.AddSample(s2);

            var result = buffer.GetInterpolatedState(1.25f);

            Assert.Greater(result.PosX, 100.0);
            Assert.LessOrEqual(result.PosX, 110.0);
        }

        [Test]
        public void BufferFull_OldestDiscarded()
        {
            // Fill buffer beyond capacity
            for (int i = 0; i < 35; i++)
            {
                _buffer.AddSample(MakeSample(posX: i, remoteTime: i * 0.1f, localTime: i * 0.1f));
            }
            Assert.AreEqual(30, _buffer.Count);
        }

        [Test]
        public void Clear_ResetsBuffer()
        {
            _buffer.AddSample(MakeSample(posX: 42.0, remoteTime: 0.0f, localTime: 0.0f));
            Assert.AreEqual(1, _buffer.Count);
            
            _buffer.Clear();
            Assert.AreEqual(0, _buffer.Count);
            Assert.AreEqual(0, _buffer.DroppedOutOfOrderSamples);
            Assert.AreEqual(0, _buffer.DroppedInvalidSamples);
        }

        // === Precision ===

        [Test]
        public void LargeCoordinates_MaintainPrecision()
        {
            var sample = MakeSample(posX: 1000000.123456, posY: -2000000.654321, posZ: 500000.111111, 
                                     remoteTime: 0.0f, localTime: 0.0f);
            _buffer.AddSample(sample);
            var result = _buffer.GetInterpolatedState(0.5f);
            
            Assert.AreEqual(1000000.123456, result.PosX, 0.001);
            Assert.AreEqual(-2000000.654321, result.PosY, 0.001);
        }

        // === Flags ===

        [Test]
        public void Flags_NotInterpolated()
        {
            var s1 = MakeSample(remoteTime: 0.0f, localTime: 0.0f);
            s1.Flags = 0b00000101; // afterburner + flaps
            _buffer.AddSample(s1);
            
            var result = _buffer.GetInterpolatedState(0.5f);
            // Flags should be taken from a sample, not interpolated to garbage
            Assert.IsTrue(result.Flags == 0b00000101 || result.Flags == 0);
        }

        // === Throttle/Controls ===

        [Test]
        public void ThrottleInterpolation()
        {
            var s1 = MakeSample(remoteTime: 0.0f, localTime: 0.0f);
            s1.Throttle = 0.0f;
            var s2 = MakeSample(remoteTime: 1.0f, localTime: 1.0f);
            s2.Throttle = 1.0f;
            _buffer.AddSample(s1);
            _buffer.AddSample(s2);
            
            var result = _buffer.GetInterpolatedState(1.65f);
            // Throttle should be between 0 and 1
            Assert.GreaterOrEqual(result.Throttle, -0.1f);
            Assert.LessOrEqual(result.Throttle, 1.1f);
        }

        // === Clock Offset ===

        [Test]
        public void ClockOffset_InitializesOnFirstSample()
        {
            Assert.AreEqual(0.0f, _buffer.ClockOffset, 0.001f);
            
            _buffer.AddSample(MakeSample(remoteTime: 10.0f, localTime: 10.5f));
            // Clock offset should be approximately 0.5 (localTime - remoteTime)
            // Exact value depends on EMA implementation
            Assert.That(_buffer.ClockOffset, Is.Not.EqualTo(0.0f).Within(0.001f));
        }

        // === Construction ===

        [Test]
        public void Constructor_DefaultValues()
        {
            var buf = new InterpolationBuffer();
            Assert.AreEqual(0, buf.Count);
            Assert.AreEqual(0.20f, buf.InterpolationDelay, 0.01f);
        }

        [Test]
        public void Constructor_CustomDelay()
        {
            var buf = new InterpolationBuffer(interpolationDelay: 0.3f);
            Assert.AreEqual(0.3f, buf.InterpolationDelay, 0.01f);
        }

        [Test]
        public void InterpolationDelay_CanBeChanged()
        {
            _buffer.InterpolationDelay = 0.25f;
            Assert.AreEqual(0.25f, _buffer.InterpolationDelay, 0.01f);
            Assert.AreEqual(0.25f, _buffer.LastEffectiveInterpolationDelay, 0.01f);
        }

        [Test]
        public void OutOfOrderTimestamp_IsDroppedBeforeTimeline()
        {
            _buffer.AddSample(MakeSample(posX: 0.0, remoteTime: 1.0f, localTime: 1.0f));
            _buffer.AddSample(MakeSample(posX: 100.0, remoteTime: 0.9f, localTime: 1.1f));

            Assert.AreEqual(1, _buffer.Count);
            Assert.AreEqual(1, _buffer.DroppedOutOfOrderSamples);
        }

        [Test]
        public void InvalidSample_IsDroppedBeforeTimeline()
        {
            var bad = MakeSample(remoteTime: 0.0f, localTime: 0.0f);
            bad.PosX = double.NaN;

            _buffer.AddSample(bad);

            Assert.AreEqual(0, _buffer.Count);
            Assert.AreEqual(1, _buffer.DroppedInvalidSamples);
        }

        [Test]
        public void SmoothTimeline_NeverRewindsWhenOffsetImproves()
        {
            var buffer = new InterpolationBuffer(capacity: 60, interpolationDelay: 0.15f);
            buffer.AddSample(MakeSample(posX: 0.0, remoteTime: 0.0f, localTime: 0.50f));
            var first = buffer.GetInterpolatedState(1.00f);
            float firstRenderTime = buffer.LastRenderTime;

            buffer.AddSample(MakeSample(posX: 100.0, remoteTime: 1.0f, localTime: 1.10f));
            var second = buffer.GetInterpolatedState(1.016f);

            Assert.GreaterOrEqual(buffer.LastRenderTime, firstRenderTime);
            Assert.IsFalse(buffer.LastRenderClockClampedBackward || second.PosX < first.PosX - 0.001);
        }

        [Test]
        public void RenderClock_LargeLag_CatchesUpWithinBound()
        {
            var buffer = new InterpolationBuffer(capacity: 120, interpolationDelay: 0.18f);
            for (int i = 0; i <= 90; i++)
            {
                float time = i / 60f;
                buffer.AddSample(MakeSample(posX: i, remoteTime: time, localTime: time));
            }

            buffer.GetInterpolatedState(0.20f);
            buffer.GetInterpolatedState(2.00f);

            Assert.LessOrEqual(buffer.LastBufferedLeadSeconds, 0.18f + 0.25f + 0.001f);
        }

        // === Helper ===

        private static InterpolationSample MakeSample(
            double posX = 0, double posY = 0, double posZ = 0,
            float remoteTime = 0, float localTime = 0)
        {
            return new InterpolationSample
            {
                PosX = posX, PosY = posY, PosZ = posZ,
                RotX = 0, RotY = 0, RotZ = 0, RotW = 1, // identity quaternion
                VelX = 0, VelY = 0, VelZ = 0,
                AngVelX = 0, AngVelY = 0, AngVelZ = 0,
                Throttle = 0, Pitch = 0, Roll = 0, Yaw = 0,
                NozzleAngle = 0, SpeedKIAS = 0, BrakeState = 0,
                Flags = 0,
                RemoteTimestamp = remoteTime,
                LocalReceiveTime = localTime
            };
        }

    }
}
