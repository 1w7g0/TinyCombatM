using UnityEngine;

namespace TCAMultiplayer.Sync
{
    /// <summary>Small in-world LineRenderer overlay for remote aircraft motion debugging.</summary>
    internal sealed class RemoteMotionDebugVisualizer
    {
        private const int VelocityLine = 0;
        private const int ForwardLine = 1;
        private const int AppliedStepLine = 2;
        private const int ExternalDriftLine = 3;
        private const int RigidbodyDriftLine = 4;
        private const int RendererOffsetLine = 5;
        private const int LineCount = 6;

        private readonly GameObject _root;
        private readonly LineRenderer[] _lines = new LineRenderer[LineCount];
        private readonly Material[] _materials = new Material[LineCount];

        public RemoteMotionDebugVisualizer(string name)
        {
            _root = new GameObject("TCAMP_RemoteMotionDebug_" + name);
            CreateLine(VelocityLine, "velocity", Color.cyan, 0.7f);
            CreateLine(ForwardLine, "forward", Color.green, 0.45f);
            CreateLine(AppliedStepLine, "appliedStep", Color.yellow, 0.6f);
            CreateLine(ExternalDriftLine, "externalDrift", Color.red, 0.8f);
            CreateLine(RigidbodyDriftLine, "rigidbodyDrift", Color.magenta, 0.55f);
            CreateLine(RendererOffsetLine, "rendererOffset", Color.white, 0.35f);
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        public void Update(
            Vector3 rootPosition,
            Quaternion rootRotation,
            Vector3 previousAppliedPosition,
            Vector3 prePosePosition,
            Vector3 rigidbodyPosition,
            Vector3 rendererCenter,
            Vector3 sampleVelocity,
            bool hasPreviousApplied,
            bool hasRendererCenter,
            float scale)
        {
            SetVisible(true);

            Vector3 velocityEnd = rootPosition;
            if (sampleVelocity.sqrMagnitude > 0.01f)
                velocityEnd += sampleVelocity * scale;
            SetLine(VelocityLine, rootPosition, velocityEnd);

            SetLine(ForwardLine, rootPosition, rootPosition + rootRotation * Vector3.forward * 35f);

            if (hasPreviousApplied)
            {
                SetLine(AppliedStepLine, previousAppliedPosition, rootPosition);
                SetLine(ExternalDriftLine, previousAppliedPosition, prePosePosition);
            }
            else
            {
                SetLine(AppliedStepLine, rootPosition, rootPosition);
                SetLine(ExternalDriftLine, rootPosition, rootPosition);
            }

            SetLine(RigidbodyDriftLine, rootPosition, rigidbodyPosition);

            if (hasRendererCenter)
                SetLine(RendererOffsetLine, rootPosition, rendererCenter);
            else
                SetLine(RendererOffsetLine, rootPosition, rootPosition);
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);

            foreach (var material in _materials)
            {
                if (material != null)
                    Object.Destroy(material);
            }
        }

        private void CreateLine(int index, string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;

            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Hidden/Internal-Colored")
                         ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader != null)
            {
                var material = new Material(shader);
                material.color = color;
                line.material = material;
                _materials[index] = material;
            }

            _lines[index] = line;
        }

        private void SetLine(int index, Vector3 start, Vector3 end)
        {
            var line = _lines[index];
            if (line == null)
                return;

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }
}
