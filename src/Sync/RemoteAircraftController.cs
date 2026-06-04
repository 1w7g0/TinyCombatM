using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Falcon.Damage;
using Falcon.UniversalAircraft;
using Falcon.Targeting;
using TCAMultiplayer.Core;
using TCAMultiplayer.Game;

namespace TCAMultiplayer.Sync
{
    /// <summary>Applies interpolated state from InterpolationBuffer to a remote aircraft clone.</summary>
    public class RemoteAircraftController : MonoBehaviour
    {
        private const string Tag = "REMOTE";
        private static readonly bool EnableVisualChildSmoothing = false;

        private InterpolationBuffer _buffer;
        private FloatingOriginService _originService;
        private Rigidbody _rb;
        private UniAircraft _aircraft;
        private float _lastInterpDiagnosticTime;
        private float _lastDriftDiagnosticTime;
        private Vector3 _lastAppliedPosition;
        private Quaternion _lastAppliedRotation = Quaternion.identity;
        private bool _hasAppliedPose;
        private float _maxAppliedStep;
        private float _stepSum;
        private int _stepCount;
        private float _maxExpectedStep;
        private float _expectedStepSum;
        private float _maxJitterError;
        private float _jitterErrorSum;
        private float _maxAlongVelocityError;
        private float _maxLateralError;
        private float _maxSmoothingCorrection;
        private float _smoothingCorrectionSum;
        private float _maxAppliedAngleStep;
        private float _angleStepSum;
        private int _angleStepCount;
        private float _maxRendererStep;
        private float _maxRendererOffsetChange;
        private float _maxRendererSizeChange;
        private float _lastImmediateMotionLogTime;
        private float _lastDebugMotionLogTime;
        private Vector3 _lastRendererCenter;
        private Vector3 _lastRendererOffset;
        private Vector3 _lastRendererSize;
        private bool _hasRendererCenter;
        private RemoteMotionDebugVisualizer _debugVisualizer;
        private float _lastAppliedStepAlongVelocity;
        private float _lastExternalDriftAlongVelocity;
        private int _appliedVelocityAxisSignFlips;
        private int _externalVelocityAxisSignFlips;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation = Quaternion.identity;
        private bool _hasSmoothedPose;
        private Vector3 _lastSampleVelocity;
        private float _lastPresentationUpdateTime;
        private Transform[] _visualSmoothingTransforms;
        private Vector3[] _visualSmoothingBaseLocalPositions;
        private Quaternion[] _visualSmoothingBaseLocalRotations;
        private bool _visualSmoothingInitialized;

        private bool _afterburnerActive, _gearDown = true, _flapsDown;
        private bool _isFiring, _isFlareFiring, _isChaffFiring;

        private float _throttle, _pitch, _roll, _yaw;
        private float _nozzleAngle, _speedKIAS, _brakeState;
        private float _flapAngle;
        private int _lastFlapUpdateFrame = -1;
        private bool _isWeightOnWheels;

        public bool IsFiring => _isFiring;
        public bool IsFlareFiring => _isFlareFiring;
        public bool IsChaffFiring => _isChaffFiring;

        private static Type _engineFXType;
        private static FieldInfo _efxRunning, _efxAfterburn, _efxThrottle, _efxHasAfterburner;
        private static MethodInfo _efxSetThrottle, _efxSetAfterburner, _efxStartup;
        private static MethodInfo _efxConfigureAfterburner, _efxUpdateAfterburnerMesh, _efxUpdate;
        private readonly List<Component> _engineFXComponents = new List<Component>();
        private readonly HashSet<Component> _startedEngineFX = new HashSet<Component>();
        private Renderer[] _renderers;

        private static Type _animPartType;
        private static MethodInfo _animPartUpdateAuto;
        private readonly List<object> _animatedParts = new List<object>();

        private static Type _flightInputType;
        private static FieldInfo _fiPitch, _fiRoll, _fiYaw, _fiNozzle, _fiThrottle;
        private static FieldInfo _fiAfterburner, _fiBrakes, _fiAreFlapsRequested;
        private static PropertyInfo _fiPitchProp, _fiRollProp, _fiYawProp;
        private object _dummyFlightInput;

        private static Type _swingWingsType;
        private static MethodInfo _swInitMethod, _swUpdateMethod;
        private object _swingWingsComponent;

        private Component _landingGearComponent;
        private MethodInfo _gearSetLoweredMethod;

        private readonly List<ParticleSystem> _muzzleFlashSystems = new List<ParticleSystem>();
        private bool _wasFiringPrevFrame;
        private bool _wasFlareFiringPrevFrame;
        private bool _wasChaffFiringPrevFrame;

        private CountermeasureLauncher _countermeasures;
        private bool _cmInitialized, _reflectionInitialized;

        /// <summary>Called by RemoteAircraftManager after instantiation.</summary>
        public void Initialize(InterpolationBuffer buffer, FloatingOriginService originService)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _originService = originService ?? throw new ArgumentNullException(nameof(originService));
            _originService.OriginShifted += HandleOriginShift;

            _rb = GetComponent<Rigidbody>();
            _aircraft = GetComponentInParent<UniAircraft>() ?? GetComponentInChildren<UniAircraft>(true);
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.None;
                _rb.detectCollisions = true;
            }

            InitializeReflection();
            _renderers = GetComponentsInChildren<Renderer>(true);
            FindEngineFXComponents();
            FindMuzzleFlashSystems();
            FindNativeAnimationComponents();
            InitializeVisualSmoothingTargets();
            SetAfterburnerState(false);
            LogVisualDiagnostics();
            Log.Info(Tag, "RemoteAircraftController initialized");
        }

        private void HandleOriginShift(Vector3 shiftAmount)
        {
            if (_hasAppliedPose)
                _lastAppliedPosition += shiftAmount;
            if (_hasSmoothedPose)
                _smoothedPosition += shiftAmount;
            if (_hasRendererCenter)
                _lastRendererCenter += shiftAmount;

            _maxAppliedStep = 0f;
            _stepSum = 0f;
            _stepCount = 0;
            _maxExpectedStep = 0f;
            _expectedStepSum = 0f;
            _maxJitterError = 0f;
            _jitterErrorSum = 0f;
            _maxAlongVelocityError = 0f;
            _maxLateralError = 0f;
            _maxSmoothingCorrection = 0f;
            _smoothingCorrectionSum = 0f;
            _maxAppliedAngleStep = 0f;
            _angleStepSum = 0f;
            _angleStepCount = 0;
            _maxRendererStep = 0f;
            _maxRendererOffsetChange = 0f;
            _maxRendererSizeChange = 0f;
            _lastDriftDiagnosticTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (_rb == null || _rb.isKinematic)
                return;

            var sample = _buffer?.GetInterpolatedState(Time.fixedTime) ?? default;
            _rb.velocity = new Vector3(sample.VelX, sample.VelY, sample.VelZ);
            _rb.angularVelocity = new Vector3(sample.AngVelX, sample.AngVelY, sample.AngVelZ);
        }

        private void Update()
        {
            UpdateGunFireEffects();
            UpdateCountermeasures();
        }

        private void LateUpdate()
        {
            RemoteMotionDebug.Poll();
            DiagnoseExternalTransformDrift();
            UpdateInterpolatedPose(Time.time);

            // UniAircraft.Update resets pilot-bypassed controls. Re-apply after it
            // so engine state, afterburners, and VTOL visuals match the remote owner.
            UpdateControlSurfaces();
            UpdateEngineThrottle();
        }

        private void UpdateInterpolatedPose(float localTime)
        {
            if (_buffer == null || _originService == null) return;

            var sample = _buffer.GetInterpolatedState(localTime);
            var localPos = _originService.AbsoluteToLocal(sample.PosX, sample.PosY, sample.PosZ);
            var rotation = NormalizeRotation(new Quaternion(sample.RotX, sample.RotY, sample.RotZ, sample.RotW));
            var sampleVelocity = new Vector3(sample.VelX, sample.VelY, sample.VelZ);
            var sampleAngularVelocity = new Vector3(sample.AngVelX, sample.AngVelY, sample.AngVelZ);
            var prePosePosition = transform.position;
            var prePoseRotation = transform.rotation;
            var rbPositionBefore = _rb != null ? _rb.position : prePosePosition;
            var rbVelocityBefore = _rb != null ? _rb.velocity : Vector3.zero;
            var rbAngularVelocityBefore = _rb != null ? _rb.angularVelocity : Vector3.zero;
            bool hadAppliedPose = _hasAppliedPose;
            var previousAppliedPosition = _lastAppliedPosition;
            float presentationDeltaTime = GetPresentationDeltaTime(localTime);
            var smoothedPosition = ComputeSmoothedPosition(
                localPos,
                rotation,
                sampleVelocity,
                presentationDeltaTime,
                hadAppliedPose,
                previousAppliedPosition);
            var smoothedRotation = ComputeSmoothedRotation(rotation, presentationDeltaTime);

            if (_rb != null)
            {
                _rb.position = localPos;
                _rb.rotation = rotation;
                if (_rb.isKinematic)
                {
                    // Kinematic keeps physics from integrating the puppet, but
                    // Target/Viewable/native helpers still read these velocities.
                    _rb.velocity = sampleVelocity;
                    _rb.angularVelocity = sampleAngularVelocity;
                }
                else
                {
                    _rb.velocity = sampleVelocity;
                    _rb.angularVelocity = sampleAngularVelocity;
                }
            }

            transform.SetPositionAndRotation(localPos, rotation);
            ApplyVisualSmoothing(localPos, rotation, smoothedPosition, smoothedRotation);

            UpdateDebugDiagnostics(
                prePosePosition,
                prePoseRotation,
                rbPositionBefore,
                rbVelocityBefore,
                rbAngularVelocityBefore,
                previousAppliedPosition,
                hadAppliedPose,
                localPos,
                rotation,
                smoothedPosition,
                sample);

            TrackAppliedStep(localPos, sampleVelocity, presentationDeltaTime, smoothedPosition);
            TrackAppliedRotation(rotation);
            TrackRendererMotion(localPos);
            _lastAppliedPosition = localPos;
            _lastAppliedRotation = rotation;
            _hasAppliedPose = true;
            _lastSampleVelocity = sampleVelocity;

            LogInterpolationDiagnostics(localPos, sample);

            _throttle = sample.Throttle; _pitch = sample.Pitch;
            _roll = sample.Roll; _yaw = sample.Yaw;
            _nozzleAngle = sample.NozzleAngle; _speedKIAS = sample.SpeedKIAS;
            _brakeState = sample.BrakeState;
            ApplyFlags(sample.Flags);
        }

        private float GetPresentationDeltaTime(float localTime)
        {
            if (_lastPresentationUpdateTime <= 0f)
            {
                _lastPresentationUpdateTime = localTime;
                return Time.deltaTime > 0f ? Time.deltaTime : 1f / 60f;
            }

            float dt = localTime - _lastPresentationUpdateTime;
            _lastPresentationUpdateTime = localTime;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                return Time.deltaTime > 0f ? Time.deltaTime : 1f / 60f;
            return Mathf.Min(dt, 0.10f);
        }

        private Vector3 ComputeSmoothedPosition(
            Vector3 authoritativePosition,
            Quaternion authoritativeRotation,
            Vector3 sampleVelocity,
            float deltaTime,
            bool hadAppliedPose,
            Vector3 previousAppliedPosition)
        {
            bool smoothingEnabled = EnableVisualChildSmoothing
                && (ModConfig.RemoteVelocityAwareSmoothingEnabled?.Value ?? true);
            if (!smoothingEnabled || !hadAppliedPose)
            {
                _smoothedPosition = authoritativePosition;
                _smoothedRotation = authoritativeRotation;
                _hasSmoothedPose = true;
                return authoritativePosition;
            }

            if (!_hasSmoothedPose)
            {
                _smoothedPosition = previousAppliedPosition;
                _hasSmoothedPose = true;
            }

            Vector3 expectedVelocity = sampleVelocity.sqrMagnitude > 0.01f
                ? sampleVelocity
                : _lastSampleVelocity;

            _smoothedPosition += expectedVelocity * deltaTime;

            float smoothingTime = ModConfig.RemoteVelocityAwareSmoothingTimeSeconds?.Value ?? 0.12f;
            smoothingTime = Mathf.Clamp(smoothingTime, 0.03f, 0.50f);
            float alpha = 1f - Mathf.Exp(-deltaTime / smoothingTime);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, authoritativePosition, alpha);

            float maxOffset = ModConfig.RemoteVisualSmoothingMaxOffsetMeters?.Value ?? 2f;
            maxOffset = Mathf.Clamp(maxOffset, 0.25f, 10f);
            Vector3 offset = _smoothedPosition - authoritativePosition;
            if (offset.magnitude > maxOffset)
                _smoothedPosition = authoritativePosition + offset.normalized * maxOffset;

            return _smoothedPosition;
        }

        private Quaternion ComputeSmoothedRotation(Quaternion authoritativeRotation, float deltaTime)
        {
            bool smoothingEnabled = EnableVisualChildSmoothing
                && (ModConfig.RemoteVelocityAwareSmoothingEnabled?.Value ?? true);
            if (!smoothingEnabled || !_hasSmoothedPose)
            {
                _smoothedRotation = authoritativeRotation;
                return authoritativeRotation;
            }

            float smoothingTime = ModConfig.RemoteVelocityAwareSmoothingTimeSeconds?.Value ?? 0.12f;
            smoothingTime = Mathf.Clamp(smoothingTime, 0.03f, 0.50f);
            float alpha = 1f - Mathf.Exp(-deltaTime / smoothingTime);
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, authoritativeRotation, alpha);
            return _smoothedRotation;
        }

        private void TrackAppliedStep(
            Vector3 localPos,
            Vector3 sampleVelocity,
            float deltaTime,
            Vector3 smoothedPosition)
        {
            if (!_hasAppliedPose)
                return;

            Vector3 actualStep = localPos - _lastAppliedPosition;
            float step = actualStep.magnitude;
            if (step > _maxAppliedStep)
                _maxAppliedStep = step;
            _stepSum += step;
            _stepCount++;

            float expectedStep = sampleVelocity.magnitude * Mathf.Max(0f, deltaTime);
            if (expectedStep > _maxExpectedStep)
                _maxExpectedStep = expectedStep;
            _expectedStepSum += expectedStep;

            Vector3 expectedDelta = sampleVelocity * Mathf.Max(0f, deltaTime);
            Vector3 error = actualStep - expectedDelta;
            float jitterError = error.magnitude;
            if (jitterError > _maxJitterError)
                _maxJitterError = jitterError;
            _jitterErrorSum += jitterError;

            Vector3 axis = sampleVelocity.sqrMagnitude > 0.01f
                ? sampleVelocity.normalized
                : transform.forward;
            float alongError = Mathf.Abs(Vector3.Dot(error, axis));
            float lateralError = (error - axis * Vector3.Dot(error, axis)).magnitude;
            if (alongError > _maxAlongVelocityError)
                _maxAlongVelocityError = alongError;
            if (lateralError > _maxLateralError)
                _maxLateralError = lateralError;

            float smoothingCorrection = Vector3.Distance(localPos, smoothedPosition);
            if (smoothingCorrection > _maxSmoothingCorrection)
                _maxSmoothingCorrection = smoothingCorrection;
            _smoothingCorrectionSum += smoothingCorrection;
        }

        private void TrackAppliedRotation(Quaternion rotation)
        {
            if (!_hasAppliedPose)
                return;

            float angleStep = Quaternion.Angle(rotation, _lastAppliedRotation);
            if (angleStep > _maxAppliedAngleStep)
                _maxAppliedAngleStep = angleStep;
            _angleStepSum += angleStep;
            _angleStepCount++;

            if (angleStep > 25f && Time.time - _lastImmediateMotionLogTime > 0.25f)
            {
                _lastImmediateMotionLogTime = Time.time;
                Log.Warning(Tag, $"Immediate rotation jump: angleStep={angleStep:F1}deg " +
                                 $"pos={transform.position} euler={rotation.eulerAngles}");
            }
        }

        private void TrackRendererMotion(Vector3 rootPosition)
        {
            if (!TryGetRendererBounds(out var bounds))
                return;

            var center = bounds.center;
            var offset = center - rootPosition;
            if (_hasRendererCenter)
            {
                float rendererStep = Vector3.Distance(center, _lastRendererCenter);
                float offsetChange = Vector3.Distance(offset, _lastRendererOffset);
                float sizeChange = Vector3.Distance(bounds.size, _lastRendererSize);
                if (rendererStep > _maxRendererStep)
                    _maxRendererStep = rendererStep;
                if (offsetChange > _maxRendererOffsetChange)
                    _maxRendererOffsetChange = offsetChange;
                if (sizeChange > _maxRendererSizeChange)
                    _maxRendererSizeChange = sizeChange;

                if ((rendererStep > 25f || offsetChange > 5f || sizeChange > 10f) && Time.time - _lastImmediateMotionLogTime > 0.25f)
                {
                    _lastImmediateMotionLogTime = Time.time;
                    Log.Warning(Tag, $"Immediate renderer jump: rendererStep={rendererStep:F2}m " +
                                     $"offsetChange={offsetChange:F2}m sizeChange={sizeChange:F2}m " +
                                     $"rootStep={Vector3.Distance(rootPosition, _lastAppliedPosition):F2}m " +
                                     $"root={rootPosition} rendererCenter={center} offset={offset} size={bounds.size}");
                }
            }

            _lastRendererCenter = center;
            _lastRendererOffset = offset;
            _lastRendererSize = bounds.size;
            _hasRendererCenter = true;
        }

        private void InitializeVisualSmoothingTargets()
        {
            if (!EnableVisualChildSmoothing)
            {
                _visualSmoothingTransforms = new Transform[0];
                _visualSmoothingBaseLocalPositions = new Vector3[0];
                _visualSmoothingBaseLocalRotations = new Quaternion[0];
                _visualSmoothingInitialized = true;
                Log.Info(Tag, "Visual child smoothing disabled");
                return;
            }

            var targets = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!IsTrackedVisualRenderer(renderer))
                    continue;

                var visualRoot = FindVisualSmoothingRoot(renderer.transform);
                if (visualRoot == null || visualRoot == transform)
                    continue;
                if (HasAuthorityComponentInSubtree(visualRoot))
                    continue;
                if (!targets.Contains(visualRoot))
                    targets.Add(visualRoot);
            }

            _visualSmoothingTransforms = targets.ToArray();
            _visualSmoothingBaseLocalPositions = new Vector3[_visualSmoothingTransforms.Length];
            _visualSmoothingBaseLocalRotations = new Quaternion[_visualSmoothingTransforms.Length];
            for (int i = 0; i < _visualSmoothingTransforms.Length; i++)
            {
                _visualSmoothingBaseLocalPositions[i] = _visualSmoothingTransforms[i].localPosition;
                _visualSmoothingBaseLocalRotations[i] = _visualSmoothingTransforms[i].localRotation;
            }

            _visualSmoothingInitialized = true;
            Log.Info(Tag, $"Visual smoothing targets: {_visualSmoothingTransforms.Length}");
        }

        private Transform FindVisualSmoothingRoot(Transform child)
        {
            if (child == null)
                return null;

            Transform candidate = child;
            Transform current = child;
            while (current != null && current.parent != null && current.parent != transform)
            {
                if (HasAuthorityComponent(current))
                    break;

                candidate = current.parent;
                current = current.parent;
            }

            if (candidate == transform || HasAuthorityComponent(candidate))
                return HasAuthorityComponent(child) ? null : child;
            return candidate;
        }

        private static bool HasAuthorityComponent(Transform t)
        {
            if (t == null)
                return false;

            return t.GetComponent<Rigidbody>() != null
                || t.GetComponent<Collider>() != null
                || t.GetComponent<Damageable>() != null
                || t.GetComponent<Target>() != null;
        }

        private static bool HasAuthorityComponentInSubtree(Transform t)
        {
            if (t == null)
                return true;

            return t.GetComponentInChildren<Rigidbody>(true) != null
                || t.GetComponentInChildren<Collider>(true) != null
                || t.GetComponentInChildren<Damageable>(true) != null
                || t.GetComponentInChildren<Target>(true) != null;
        }

        private void ApplyVisualSmoothing(
            Vector3 authoritativePosition,
            Quaternion authoritativeRotation,
            Vector3 smoothedPosition,
            Quaternion smoothedRotation)
        {
            // Child-offset smoothing can split cockpit/canopy/model subtrees from
            // the authoritative aircraft root on some aircraft hierarchies.
            ResetVisualSmoothingTargets();
        }

        private void ResetVisualSmoothingTargets()
        {
            if (!_visualSmoothingInitialized || _visualSmoothingTransforms == null)
                return;

            for (int i = 0; i < _visualSmoothingTransforms.Length; i++)
            {
                var target = _visualSmoothingTransforms[i];
                if (target == null)
                    continue;
                target.localPosition = _visualSmoothingBaseLocalPositions[i];
                target.localRotation = _visualSmoothingBaseLocalRotations[i];
            }
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            if (_renderers == null || _renderers.Length == 0)
                return false;

            bool hasBounds = false;
            foreach (var renderer in _renderers)
            {
                if (!IsTrackedVisualRenderer(renderer))
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                return false;

            return true;
        }

        private static bool IsTrackedVisualRenderer(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled)
                return false;

            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                return false;

            return true;
        }

        private void DiagnoseExternalTransformDrift()
        {
            if (!_hasAppliedPose || Time.time - _lastDriftDiagnosticTime < 1f)
                return;

            float drift = Vector3.Distance(transform.position, _lastAppliedPosition);
            float angleDrift = Quaternion.Angle(transform.rotation, _lastAppliedRotation);
            if (drift > 0.05f || angleDrift > 0.5f)
            {
                _lastDriftDiagnosticTime = Time.time;
                Log.Warning(Tag, $"External transform drift before LateUpdate: pos={drift:F3}m rot={angleDrift:F2}deg " +
                                 $"current={transform.position} lastApplied={_lastAppliedPosition}");
            }
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float sqrMagnitude =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;
            if (sqrMagnitude < 0.000001f)
                return Quaternion.identity;

            if (Mathf.Abs(1f - sqrMagnitude) > 0.0001f)
            {
                float invMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
                rotation.x *= invMagnitude;
                rotation.y *= invMagnitude;
                rotation.z *= invMagnitude;
                rotation.w *= invMagnitude;
            }

            return rotation;
        }

        private void ApplyFlags(byte flags)
        {
            // Bit 0:Afterburner 1:GearDown 2:FlapsDown 3:IsFiring 4:Flare 5:Chaff
            bool ab = (flags & (1 << 0)) != 0 && HasAfterburner();
            bool gear = (flags & (1 << 1)) != 0;
            _flapsDown     = (flags & (1 << 2)) != 0;
            _isFiring      = (flags & (1 << 3)) != 0;
            _isFlareFiring = (flags & (1 << 4)) != 0;
            _isChaffFiring = (flags & (1 << 5)) != 0;
            _isWeightOnWheels = (flags & (1 << 7)) != 0;

            if (ab != _afterburnerActive) { _afterburnerActive = ab; SetAfterburnerState(ab); }
            if (gear != _gearDown) { _gearDown = gear; SetGearState(gear); }
        }

        private void LogInterpolationDiagnostics(Vector3 localPos, InterpolationSample sample)
        {
            if (Time.time - _lastInterpDiagnosticTime < 5f)
                return;
            _lastInterpDiagnosticTime = Time.time;

            if (_buffer == null || !_buffer.TryGetSampleStats(out int count, out float remoteSpan, out float localSpan))
            {
                Log.Warning(Tag, "Interp diag: buffer empty");
                return;
            }

            float sampleRate = localSpan > 0.001f && count > 1 ? (count - 1) / localSpan : 0f;
            bool hasDetailed = _buffer.TryGetDetailedStats(out var stats);
            float newestAge = hasDetailed ? Time.time - stats.NewestLocalReceiveTime : 0f;
            float avgStep = _stepCount > 0 ? _stepSum / _stepCount : 0f;
            float avgExpectedStep = _stepCount > 0 ? _expectedStepSum / _stepCount : 0f;
            float avgJitterError = _stepCount > 0 ? _jitterErrorSum / _stepCount : 0f;
            float avgSmoothingCorrection = _stepCount > 0 ? _smoothingCorrectionSum / _stepCount : 0f;
            float avgAngleStep = _angleStepCount > 0 ? _angleStepSum / _angleStepCount : 0f;
            float extrapMs = _buffer.LastExtrapolationSeconds * 1000f;

            if (count < 3 || sampleRate < 45f || localSpan - remoteSpan > 0.25f
                || newestAge > 0.15f || _buffer.LastMode != InterpolationMode.Interpolate
                || _maxAppliedStep > 4f || _maxAppliedAngleStep > 10f
                || _maxJitterError > 3f || _maxRendererOffsetChange > 1f || _maxRendererSizeChange > 10f)
            {
                Log.Warning(Tag, $"Motion diag: mode={_buffer.LastMode} count={count} recvHz={sampleRate:F1} " +
                                 $"remoteSpan={remoteSpan:F2}s localSpan={localSpan:F2}s newestAge={newestAge * 1000f:F0}ms " +
                                 $"renderLag={(stats.NewestRemoteTime - _buffer.LastRenderTime) * 1000f:F0}ms " +
                                 $"effDelay={_buffer.LastEffectiveInterpolationDelay * 1000f:F0}ms " +
                                 $"lead={_buffer.LastBufferedLeadSeconds * 1000f:F0}ms " +
                                 $"maxRemoteGap={stats.MaxRemoteGap * 1000f:F0}ms maxRecvGap={stats.MaxLocalGap * 1000f:F0}ms " +
                                 $"extrap={extrapMs:F0}ms avgStep={avgStep:F2}m maxStep={_maxAppliedStep:F2}m " +
                                 $"avgExpected={avgExpectedStep:F2}m maxExpected={_maxExpectedStep:F2}m " +
                                 $"avgJitterErr={avgJitterError:F2}m maxJitterErr={_maxJitterError:F2}m " +
                                 $"maxAlongErr={_maxAlongVelocityError:F2}m maxLatErr={_maxLateralError:F2}m " +
                                 $"avgSmoothCorr={avgSmoothingCorrection:F2}m maxSmoothCorr={_maxSmoothingCorrection:F2}m " +
                                 $"clockErr={_buffer.LastRenderClockError * 1000f:F0}ms clockCorr={_buffer.LastRenderClockCorrection * 1000f:F1}ms " +
                                 $"clockLarge={_buffer.LastRenderClockLargeError} clockClampBack={_buffer.LastRenderClockClampedBackward} " +
                                 $"droppedOld={_buffer.DroppedOutOfOrderSamples} droppedBad={_buffer.DroppedInvalidSamples} " +
                                 $"avgRot={avgAngleStep:F2}deg maxRot={_maxAppliedAngleStep:F2}deg " +
                                 $"maxRendererStep={_maxRendererStep:F2}m maxRendererOffset={_maxRendererOffsetChange:F2}m " +
                                 $"maxRendererSize={_maxRendererSizeChange:F2}m pos={localPos} remoteTime={sample.RemoteTimestamp:F2}");
            }

            _maxAppliedStep = 0f;
            _stepSum = 0f;
            _stepCount = 0;
            _maxExpectedStep = 0f;
            _expectedStepSum = 0f;
            _maxJitterError = 0f;
            _jitterErrorSum = 0f;
            _maxAlongVelocityError = 0f;
            _maxLateralError = 0f;
            _maxSmoothingCorrection = 0f;
            _smoothingCorrectionSum = 0f;
            _maxAppliedAngleStep = 0f;
            _angleStepSum = 0f;
            _angleStepCount = 0;
            _maxRendererStep = 0f;
            _maxRendererOffsetChange = 0f;
            _maxRendererSizeChange = 0f;
        }

        private void UpdateDebugDiagnostics(
            Vector3 prePosePosition,
            Quaternion prePoseRotation,
            Vector3 rbPositionBefore,
            Vector3 rbVelocityBefore,
            Vector3 rbAngularVelocityBefore,
            Vector3 previousAppliedPosition,
            bool hadAppliedPose,
            Vector3 appliedPosition,
            Quaternion appliedRotation,
            Vector3 smoothedPosition,
            InterpolationSample sample)
        {
            if (!RemoteMotionDebug.Enabled)
            {
                _debugVisualizer?.SetVisible(false);
                return;
            }

            var sampleVelocity = new Vector3(sample.VelX, sample.VelY, sample.VelZ);
            var velocityAxis = sampleVelocity.sqrMagnitude > 0.01f
                ? sampleVelocity.normalized
                : appliedRotation * Vector3.forward;

            Vector3 appliedStep = hadAppliedPose ? appliedPosition - previousAppliedPosition : Vector3.zero;
            Vector3 externalDrift = hadAppliedPose ? prePosePosition - previousAppliedPosition : Vector3.zero;
            float appliedAlongVelocity = Vector3.Dot(appliedStep, velocityAxis);
            float externalAlongVelocity = Vector3.Dot(externalDrift, velocityAxis);
            float externalLateral = (externalDrift - velocityAxis * externalAlongVelocity).magnitude;

            TrackVelocityAxisOscillation(appliedAlongVelocity, ref _lastAppliedStepAlongVelocity, ref _appliedVelocityAxisSignFlips);
            TrackVelocityAxisOscillation(externalAlongVelocity, ref _lastExternalDriftAlongVelocity, ref _externalVelocityAxisSignFlips);

            if (_debugVisualizer == null)
                _debugVisualizer = new RemoteMotionDebugVisualizer(name);

            _debugVisualizer.Update(
                appliedPosition,
                appliedRotation,
                previousAppliedPosition,
                prePosePosition,
                rbPositionBefore,
                _lastRendererCenter,
                sampleVelocity,
                hadAppliedPose,
                _hasRendererCenter,
                RemoteMotionDebug.DrawScale);

            if (Time.time - _lastDebugMotionLogTime < RemoteMotionDebug.LogIntervalSeconds)
                return;

            _lastDebugMotionLogTime = Time.time;

            int count = 0;
            float remoteSpan = 0f;
            float localSpan = 0f;
            _buffer?.TryGetSampleStats(out count, out remoteSpan, out localSpan);
            float rbPoseDrift = Vector3.Distance(rbPositionBefore, prePosePosition);
            float rotationDrift = Quaternion.Angle(prePoseRotation, _lastAppliedRotation);
            float visualCorrection = Vector3.Distance(appliedPosition, smoothedPosition);

            Log.Info(Tag,
                $"Motion debug: mode={_buffer?.LastMode.ToString() ?? "<none>"} count={count} " +
                $"remoteSpan={remoteSpan:F3}s localSpan={localSpan:F3}s " +
                $"preDrift={externalDrift.magnitude:F3}m alongVel={externalAlongVelocity:F3}m lateral={externalLateral:F3}m " +
                $"rotDrift={rotationDrift:F2}deg appliedStep={appliedStep.magnitude:F3}m appliedAlongVel={appliedAlongVelocity:F3}m " +
                $"expectedStep={sampleVelocity.magnitude * Time.deltaTime:F3}m visualCorrection={visualCorrection:F3}m " +
                $"clockErr={_buffer?.LastRenderClockError * 1000f ?? 0f:F0}ms clockCorr={_buffer?.LastRenderClockCorrection * 1000f ?? 0f:F1}ms " +
                $"vel={sampleVelocity.magnitude:F1}m/s rbVelBefore={rbVelocityBefore.magnitude:F1}m/s " +
                $"rbAngVelBefore={rbAngularVelocityBefore.magnitude:F2}rad/s rbPoseDrift={rbPoseDrift:F3}m " +
                $"rendererOffset={_lastRendererOffset.magnitude:F3}m rendererSize={_lastRendererSize} " +
                $"axisFlips=applied:{_appliedVelocityAxisSignFlips},external:{_externalVelocityAxisSignFlips} " +
                $"root={appliedPosition} pre={prePosePosition}");

            _appliedVelocityAxisSignFlips = 0;
            _externalVelocityAxisSignFlips = 0;
        }

        private static void TrackVelocityAxisOscillation(float value, ref float previousValue, ref int signFlips)
        {
            if (Mathf.Abs(value) > 0.03f && Mathf.Abs(previousValue) > 0.03f
                && Mathf.Sign(value) != Mathf.Sign(previousValue))
            {
                signFlips++;
            }

            previousValue = value;
        }
        private void SetAfterburnerState(bool active)
        {
            // Visual engine state is applied once per frame after pose interpolation.
        }

        private void ApplyRemoteFlightControls()
        {
            if (_aircraft == null) return;

            var input = _aircraft.FlightControls;
            input.Throttle = _throttle;
            input.Afterburner = GetAfterburnerThrottle();
            input.NozzleAnalog = GetNozzleAnalog();
            input.AreFlapsRequested = _flapsDown;
            input.Brakes = _brakeState;
            input.IsEngineOn = true;
            _aircraft.FlightControls = input;
        }

        private void UpdateEngineThrottle()
        {
            float afterburnerThrottle = GetAfterburnerThrottle();

            if (_aircraft?.Engines != null)
            {
                ApplyRemoteFlightControls();
                foreach (var engine in _aircraft.Engines)
                {
                    try
                    {
                        if (engine == null) continue;
                        engine.SetEnabledForAllInstancesOfEngine(true);
                        engine.UpdateEngineSpooling(Time.deltaTime);
                        engine.UpdateThrottles(_throttle, afterburnerThrottle);
                        if (engine.VTOLNozzleMax > 0f)
                        {
                            engine.VTOLNozzleAngle = Mathf.Clamp(_nozzleAngle, 0f, engine.VTOLNozzleMax);
                            foreach (var part in engine.OwnedVTOLAnimatedParts)
                                part?.UpdateManually(-engine.VTOLNozzleAngle, Time.deltaTime);
                        }
                        engine.UpdateEngine(Time.deltaTime);
                        engine.SetIsPlayer(false);
                    }
                    catch { }
                }
            }

            foreach (var efx in _engineFXComponents)
            {
                if (efx == null) continue;
                try
                {
                    bool efxHasAfterburner = EngineFxHasAfterburner(efx);
                    float efxAfterburnerThrottle = efxHasAfterburner ? afterburnerThrottle : 0f;

                    if (!_startedEngineFX.Contains(efx))
                    {
                        _efxStartup?.Invoke(efx, null);
                        if (efxHasAfterburner)
                            _efxConfigureAfterburner?.Invoke(efx, null);
                        _startedEngineFX.Add(efx);
                    }
                    _efxRunning?.SetValue(efx, true);
                    if (_efxSetThrottle != null)
                        _efxSetThrottle.Invoke(efx, new object[] { _throttle });
                    else
                        _efxThrottle?.SetValue(efx, _throttle);

                    if (_efxSetAfterburner != null)
                        _efxSetAfterburner.Invoke(efx, new object[] { efxAfterburnerThrottle });
                    else
                        _efxAfterburn?.SetValue(efx, efxAfterburnerThrottle);

                    _efxUpdateAfterburnerMesh?.Invoke(efx, new object[] { efxAfterburnerThrottle });
                    _efxUpdate?.Invoke(efx, null);
                }
                catch { }
            }
        }

        private bool HasAfterburner()
        {
            if (_aircraft?.Engines == null) return false;
            foreach (var engine in _aircraft.Engines)
                if (engine != null && engine.HasAfterburner)
                    return true;
            return false;
        }

        private float GetAfterburnerThrottle()
        {
            return _afterburnerActive && HasAfterburner() ? 1f : 0f;
        }

        private float GetNozzleAnalog()
        {
            float maxNozzleAngle = GetMaxNozzleAngle();
            return maxNozzleAngle > 0.01f
                ? Mathf.Clamp01(_nozzleAngle / maxNozzleAngle)
                : 0f;
        }

        private float GetMaxNozzleAngle()
        {
            if (_aircraft?.Engines == null) return 0f;

            float max = 0f;
            foreach (var engine in _aircraft.Engines)
                if (engine != null && engine.VTOLNozzleMax > max)
                    max = engine.VTOLNozzleMax;
            return max;
        }

        private static bool EngineFxHasAfterburner(Component efx)
        {
            if (efx == null || _efxHasAfterburner == null)
                return false;

            try { return Convert.ToBoolean(_efxHasAfterburner.GetValue(efx)); }
            catch { return false; }
        }

        private float GetFlapAngle()
        {
            if (_lastFlapUpdateFrame == Time.frameCount)
            {
                if (_aircraft?.Flaps != null)
                    _aircraft.Flaps.Angle = _flapAngle;
                return _flapAngle;
            }

            _lastFlapUpdateFrame = Time.frameCount;

            try
            {
                if (_aircraft?.Flaps != null)
                {
                    _aircraft.Flaps.Update(_speedKIAS, _nozzleAngle, _flapsDown, Time.deltaTime);
                    _flapAngle = _aircraft.Flaps.Angle;
                    return _flapAngle;
                }
            }
            catch { }

            _flapAngle = _flapsDown ? 25f : 0f;
            return _flapAngle;
        }

        private void SetGearState(bool down)
        {
            if (_landingGearComponent == null || _gearSetLoweredMethod == null) return;
            try { _gearSetLoweredMethod.Invoke(_landingGearComponent, new object[] { down }); }
            catch (Exception ex) { Log.Warning(Tag, $"SetGearState: {ex.Message}"); }
        }
        private void UpdateControlSurfaces()
        {
            if (_animatedParts.Count > 0 && _animPartUpdateAuto != null)
            {
                EnsureDummyFlightInput();
                if (_dummyFlightInput != null)
                {
                    SetFlightInputAxis(_fiPitchProp, _fiPitch, _pitch);
                    SetFlightInputAxis(_fiRollProp, _fiRoll, _roll);
                    SetFlightInputAxis(_fiYawProp, _fiYaw, _yaw);
                    _fiNozzle?.SetValue(_dummyFlightInput, GetNozzleAnalog());
                    _fiThrottle?.SetValue(_dummyFlightInput, _throttle);
                    _fiAfterburner?.SetValue(_dummyFlightInput, GetAfterburnerThrottle());
                    _fiBrakes?.SetValue(_dummyFlightInput, _brakeState);
                    _fiAreFlapsRequested?.SetValue(_dummyFlightInput, _flapsDown);

                    float flapAngle = GetFlapAngle();
                    foreach (var part in _animatedParts)
                    {
                        try
                        {
                            _animPartUpdateAuto.Invoke(part, new object[]
                                { _dummyFlightInput, flapAngle, _speedKIAS, _brakeState, _isWeightOnWheels, Time.deltaTime });
                        }
                        catch { }
                    }
                }
            }

            if (_swingWingsComponent != null && _swUpdateMethod != null)
            {
                try { _swUpdateMethod.Invoke(_swingWingsComponent, new object[] { Time.deltaTime, _speedKIAS }); }
                catch { }
            }
        }

        private void EnsureDummyFlightInput()
        {
            if (_dummyFlightInput != null || _flightInputType == null) return;
            try { _dummyFlightInput = Activator.CreateInstance(_flightInputType, new object[] { true }); }
            catch (Exception ex) { Log.Warning(Tag, $"FlightInput create failed: {ex.Message}"); }
        }

        private void SetFlightInputAxis(PropertyInfo property, FieldInfo field, float value)
        {
            try
            {
                if (property != null)
                    property.SetValue(_dummyFlightInput, value);
                else
                    field?.SetValue(_dummyFlightInput, value);
            }
            catch { }
        }

        private void UpdateGunFireEffects()
        {
            if (_isFiring && !_wasFiringPrevFrame)
                foreach (var ps in _muzzleFlashSystems) { if (ps != null && !ps.isPlaying) ps.Play(); }
            else if (!_isFiring && _wasFiringPrevFrame)
                foreach (var ps in _muzzleFlashSystems) { if (ps != null && ps.isPlaying) ps.Stop(); }
            _wasFiringPrevFrame = _isFiring;
        }

        private void UpdateCountermeasures()
        {
            if (!_cmInitialized)
                InitializeCountermeasures();
            if (_countermeasures == null) return;

            // Edge-triggered: log rising edge for debugging.
            // The continuous flag is set every frame below because
            // UniAircraft.FixedUpdate() clears it via IsPilotBypassed → ResetControls.
            if (_isFlareFiring && !_wasFlareFiringPrevFrame)
                Log.Debug(Tag, "Flare firing started (rising edge)");
            if (_isChaffFiring && !_wasChaffFiringPrevFrame)
                Log.Debug(Tag, "Chaff firing started (rising edge)");

            // Set continuous launching flags directly on CountermeasureLauncher.
            // Must re-set every frame because UniAircraft.FixedUpdate() clears them
            // via IsPilotBypassed → ResetControls → WeaponControls.Reset() → lines
            // 354-355 copy zeroed values to CountermeasureLauncher before its CM.Update().
            _countermeasures.IsFlareContinouslyLaunching = _isFlareFiring;
            _countermeasures.IsChaffContinouslyLaunching = _isChaffFiring;

            // Drive CM processing ourselves — the FixedUpdate CM.Update() runs
            // with cleared flags (no-op), so we call it here with correct flags.
            // CountermeasureLauncher.Update() is time-gated via FireDelay, safe to
            // call multiple times per frame.
            if (_isFlareFiring || _isChaffFiring)
                _countermeasures.Update();

            _wasFlareFiringPrevFrame = _isFlareFiring;
            _wasChaffFiringPrevFrame = _isChaffFiring;
        }

        private void InitializeCountermeasures()
        {
            _cmInitialized = true;
            var ac = GetComponentInParent<UniAircraft>() ?? GetComponentInChildren<UniAircraft>(true);
            if (ac == null) return;

            // Direct API access — CountermeasureLauncher and its fields are public.
            // No reflection needed.
            _countermeasures = ac.Countermeasures;
        }

        private void InitializeReflection()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;
            var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            _engineFXType = Type.GetType("Falcon.Effects.EngineFX, Assembly-CSharp");
            if (_engineFXType != null)
            {
                _efxRunning = _engineFXType.GetField("isRunning", f);
                _efxAfterburn = _engineFXType.GetField("targetAfterburn", f);
                _efxThrottle = _engineFXType.GetField("jetThrottle", f);
                _efxHasAfterburner = _engineFXType.GetField("hasAfterburner", f);
                _efxSetThrottle = _engineFXType.GetMethod("SetThrottle", f);
                _efxSetAfterburner = _engineFXType.GetMethod("SetAfterburner", f);
                _efxStartup = _engineFXType.GetMethod("Startup", f);
                _efxConfigureAfterburner = _engineFXType.GetMethod("ConfigureAfterburner", f);
                _efxUpdateAfterburnerMesh = _engineFXType.GetMethod("UpdateAfterburnerMesh", f);
                _efxUpdate = _engineFXType.GetMethod("Update", f);
            }

            _animPartType = Type.GetType("Falcon.UniversalAircraft.UniAnimatedPart, Assembly-CSharp");
            if (_animPartType != null)
                _animPartUpdateAuto = _animPartType.GetMethod("UpdateAutomatic", f);

            _swingWingsType = Type.GetType("Falcon.UniversalAircraft.SwingWings, Assembly-CSharp");
            if (_swingWingsType != null)
            {
                _swInitMethod = _swingWingsType.GetMethod("InitializeWithData", f);
                _swUpdateMethod = _swingWingsType.GetMethod("UpdateAutoSpeed", f);
            }

            _flightInputType = Type.GetType("Falcon.Controls.FlightInput, Assembly-CSharp");
            if (_flightInputType != null)
            {
                _fiPitchProp = _flightInputType.GetProperty("Pitch", f);
                _fiRollProp = _flightInputType.GetProperty("Roll", f);
                _fiYawProp = _flightInputType.GetProperty("Yaw", f);
                _fiPitch = _flightInputType.GetField("Pitch", f) ?? _flightInputType.GetField("<Pitch>k__BackingField", f);
                _fiRoll = _flightInputType.GetField("Roll", f) ?? _flightInputType.GetField("<Roll>k__BackingField", f);
                _fiYaw = _flightInputType.GetField("Yaw", f) ?? _flightInputType.GetField("<Yaw>k__BackingField", f);
                _fiNozzle = _flightInputType.GetField("NozzleAnalog", f) ?? _flightInputType.GetField("<NozzleAnalog>k__BackingField", f);
                _fiThrottle = _flightInputType.GetField("Throttle", f) ?? _flightInputType.GetField("<Throttle>k__BackingField", f);
                _fiAfterburner = _flightInputType.GetField("Afterburner", f);
                _fiBrakes = _flightInputType.GetField("Brakes", f);
                _fiAreFlapsRequested = _flightInputType.GetField("AreFlapsRequested", f);
            }

            Log.Debug(Tag, $"Reflection: EFX={_engineFXType != null} Anim={_animPartType != null} " +
                $"SW={_swingWingsType != null} FI={_flightInputType != null}");
        }

        private void FindEngineFXComponents()
        {
            if (_engineFXType == null) return;
            foreach (var c in GetComponentsInChildren<Component>(true))
                if (c != null && _engineFXType.IsAssignableFrom(c.GetType())) _engineFXComponents.Add(c);
            Log.Debug(Tag, $"Found {_engineFXComponents.Count} EngineFX");
        }

        private void FindMuzzleFlashSystems()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                string n = ps.gameObject.name.ToLower();
                string p = ps.transform.parent?.name.ToLower() ?? "";
                if (n.Contains("muzzle") || n.Contains("flash") ||
                    p.Contains("gun") || p.Contains("barrel") || p.Contains("firepoint"))
                    _muzzleFlashSystems.Add(ps);
            }
            Log.Debug(Tag, $"Found {_muzzleFlashSystems.Count} muzzle flash systems");
        }

        private Dictionary<string, Transform> BuildTransformDict()
        {
            var dict = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (!dict.ContainsKey(t.name)) dict[t.name] = t;
            return dict;
        }

        private void FindNativeAnimationComponents()
        {
            var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // Landing gear
            var lgType = Type.GetType("Falcon.Vehicles.LandingGear, Assembly-CSharp");
            if (lgType != null)
            {
                _landingGearComponent = GetComponentInParent(lgType);
                if (_landingGearComponent != null)
                    _gearSetLoweredMethod = lgType.GetMethod("SetGearLowered", BindingFlags.Public | BindingFlags.Instance);
            }

            // UniAnimatedPart — build from UniAircraftData (UniAircraft.Start() hasn't run)
            object data = null;
            Component parentAc = null;
            Dictionary<string, Transform> tDict = null;

            if (_animPartType != null)
            {
                var dataType = Type.GetType("Falcon.UniversalAircraft.UniAircraftData, Assembly-CSharp");
                var propType = Type.GetType("Falcon.UniversalAircraft.AnimatedPartProperties, Assembly-CSharp");
                if (dataType != null && propType != null)
                {
                    var dataField = typeof(UniAircraft).GetField("Data", f);
                    var partsField = dataType.GetField("AnimatedParts", BindingFlags.Public | BindingFlags.Instance);
                    var partRefField = propType.GetField("Part", BindingFlags.Public | BindingFlags.Instance);

                    parentAc = GetComponentInParent(typeof(UniAircraft));
                    if (parentAc != null) data = dataField?.GetValue(parentAc);
                    var parts = data != null ? partsField?.GetValue(data) as System.Collections.IList : null;

                    if (parts != null)
                    {
                        tDict = BuildTransformDict();
                        foreach (var pd in parts)
                        {
                            if (pd == null) continue;
                            string tn = partRefField?.GetValue(pd)?.ToString() ?? "";
                            if (tDict.TryGetValue(tn, out Transform pt))
                            {
                                try { _animatedParts.Add(Activator.CreateInstance(_animPartType, pd, pt)); }
                                catch { }
                            }
                        }
                    }
                }
            }

            // SwingWings
            if (_swingWingsType != null && _swInitMethod != null)
            {
                if (data == null && parentAc != null)
                    data = typeof(UniAircraft).GetField("Data", f)?.GetValue(parentAc);

                if (data != null)
                {
                    var swField = data.GetType().GetField("SwingWings", BindingFlags.Public | BindingFlags.Instance);
                    var swData = swField?.GetValue(data);
                    var hasField = swData?.GetType().GetField("HasSwingWings");
                    bool hasSW = hasField != null && (bool)hasField.GetValue(swData);

                    if (hasSW)
                    {
                        try
                        {
                            _swingWingsComponent = Activator.CreateInstance(_swingWingsType);
                            if (tDict == null) tDict = BuildTransformDict();
                            _swInitMethod.Invoke(_swingWingsComponent, new object[] { swData, tDict });
                        }
                        catch (Exception ex)
                        {
                            _swingWingsComponent = null;
                            Log.Warning(Tag, $"SwingWings init: {ex.Message}");
                        }
                    }
                }
            }

            Log.Debug(Tag, $"Anim: {_animatedParts.Count} parts, Gear={_landingGearComponent != null}, SW={_swingWingsComponent != null}");
        }

        private void LogVisualDiagnostics()
        {
            string parentName = transform.parent != null ? transform.parent.name : "<none>";
            string rootName = transform.root != null ? transform.root.name : "<none>";
            int enabledBehaviours = 0;
            var behaviourNames = "";
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || !behaviour.enabled)
                    continue;

                enabledBehaviours++;
                if (enabledBehaviours <= 18)
                {
                    if (behaviourNames.Length > 0)
                        behaviourNames += ",";
                    behaviourNames += behaviour.GetType().Name;
                }
            }

            Log.Info(Tag, $"Visual diag init: object={name}, parent={parentName}, sceneRoot={rootName}, " +
                          $"isSceneRoot={transform.root == transform}, renderers={_renderers?.Length ?? 0}, " +
                          $"engineFX={_engineFXComponents.Count}, animParts={_animatedParts.Count}, " +
                          $"enabledBehaviours={enabledBehaviours} [{behaviourNames}]");
        }

        private void OnDestroy()
        {
            if (_originService != null)
                _originService.OriginShifted -= HandleOriginShift;

            _countermeasures = null;
            _dummyFlightInput = null;
            _swingWingsComponent = null;
            _animatedParts.Clear();
            _engineFXComponents.Clear();
            _startedEngineFX.Clear();
            _muzzleFlashSystems.Clear();
            _debugVisualizer?.Dispose();
            _debugVisualizer = null;
        }
    }
}
