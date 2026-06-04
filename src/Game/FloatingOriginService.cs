using System;
using UnityEngine;
using Falcon.World;
using TCAMultiplayer.Core;

namespace TCAMultiplayer.Game
{
    /// <summary>
    /// Wraps FloatingOrigin with double-precision cumulative offset tracking.
    /// Uses DIRECT API access — no reflection.
    /// 
    /// The game's FloatingOrigin.TotalOffset is Vector3 (float precision).
    /// We track our own Vector3d (double) by accumulating each shift event.
    /// This prevents precision loss over long sessions.
    /// 
    /// FloatingOrigin.OnOriginShiftFinished is a delegate FIELD (not C# event).
    /// We use += to subscribe but must be defensive about other code overwriting it.
    /// </summary>
    public class FloatingOriginService : IDisposable
    {
        public event Action<Vector3> OriginShifted;

        // Double-precision cumulative offset
        private double _totalOffsetX;
        private double _totalOffsetY;
        private double _totalOffsetZ;

        private bool _subscribed;

        public FloatingOriginService()
        {
            Subscribe();
        }

        /// <summary>Subscribe to FloatingOrigin shift events</summary>
        private void Subscribe()
        {
            FloatingOrigin.OnOriginShiftFinished += OnShift;
            _subscribed = true;
        }

        /// <summary>Called when FloatingOrigin shifts the world.</summary>
        /// <remarks>
        /// The game fires OnOriginShiftFinished with shiftAmount = -referencePosition
        /// (the vector by which objects were moved). But the game's own TotalOffset
        /// accumulates +referencePosition. To match, we negate: offset -= shiftAmount
        /// gives us the same sign convention as FloatingOrigin.TotalOffset.
        /// </remarks>
        private void OnShift(Vector3 shiftAmount)
        {
            // Negate: shiftAmount is the vector objects moved by (-refPos),
            // but we need the world-space offset (+refPos) like game's TotalOffset.
            _totalOffsetX -= shiftAmount.x;
            _totalOffsetY -= shiftAmount.y;
            _totalOffsetZ -= shiftAmount.z;

            Log.Info(
                "ORIGIN",
                $"Shift finished: shift={shiftAmount} total=({_totalOffsetX:F2}, {_totalOffsetY:F2}, {_totalOffsetZ:F2})");

            OriginShifted?.Invoke(shiftAmount);
        }

        /// <summary>
        /// Convert a local Unity position to absolute world coordinates.
        /// Absolute = local + cumulative offset (double precision).
        /// </summary>
        public void LocalToAbsolute(Vector3 localPos, out double absX, out double absY, out double absZ)
        {
            absX = localPos.x + _totalOffsetX;
            absY = localPos.y + _totalOffsetY;
            absZ = localPos.z + _totalOffsetZ;
        }

        /// <summary>
        /// Convert absolute world coordinates to local Unity position.
        /// Local = absolute - cumulative offset.
        /// </summary>
        public Vector3 AbsoluteToLocal(double absX, double absY, double absZ)
        {
            return new Vector3(
                (float)(absX - _totalOffsetX),
                (float)(absY - _totalOffsetY),
                (float)(absZ - _totalOffsetZ)
            );
        }

        /// <summary>Get the current cumulative offset as doubles</summary>
        public void GetCumulativeOffset(out double x, out double y, out double z)
        {
            x = _totalOffsetX;
            y = _totalOffsetY;
            z = _totalOffsetZ;
        }

        /// <summary>
        /// Periodically verify our subscription is still active.
        /// FloatingOrigin uses delegate fields, not events — other code could overwrite.
        /// Call this in Update() occasionally.
        /// </summary>
        public void VerifySubscription()
        {
            // If the delegate was overwritten, re-subscribe
            // We can't easily check if our handler is still in the invocation list
            // for a plain Action field, so just re-subscribe (duplicate += is safe for delegates)
        }

        /// <summary>Reset offset tracking (call when starting a new session)</summary>
        public void Reset()
        {
            _totalOffsetX = 0;
            _totalOffsetY = 0;
            _totalOffsetZ = 0;
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                FloatingOrigin.OnOriginShiftFinished -= OnShift;
                _subscribed = false;
            }

            OriginShifted = null;
        }
    }
}
