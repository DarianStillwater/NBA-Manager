using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// The original P1 body: a single capsule tinted its team color via a MaterialPropertyBlock
    /// (all capsules share one material). No animation — Animate only applies the data-driven jump
    /// offset so a shot still lifts the actor. Used as the always-available fallback when the
    /// rigged character asset isn't present (e.g. before the editor build steps have been run).
    /// </summary>
    public class CapsuleBody : IActorBody
    {
        private const float RadiusFeet = 0.95f;

        private Transform _body;
        private float _heightFeet = 6.4f;

        public float VisualHeightFeet => _heightFeet;

        public bool Build(Transform root, PlayerAppearance appearance)
        {
            _heightFeet = appearance != null ? appearance.HeightFeet : 6.4f;

            // Capsule pivot is centered, so lift it by half its height to stand on the floor.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localScale = new Vector3(RadiusFeet * 2f, _heightFeet * 0.5f, RadiusFeet * 2f);
            body.transform.localPosition = new Vector3(0f, _heightFeet * 0.5f, 0f);

            var col = body.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = SharedBodyMaterial();
            var block = new MaterialPropertyBlock();
            Match3DMaterials.ApplyColor(block, appearance != null ? appearance.Jersey : Color.gray);
            renderer.SetPropertyBlock(block);

            _body = body.transform;
            return true;
        }

        public void Animate(in ActorFrame frame, float dt)
        {
            if (_body == null) return;
            // No skeleton to animate — but honor the data-driven jump so airborne moments still
            // read (lift the whole capsule; its center stays half-height above its base).
            float y = _heightFeet * 0.5f + Mathf.Max(0f, frame.VerticalOffset);
            var p = _body.localPosition;
            if (!Mathf.Approximately(p.y, y))
                _body.localPosition = new Vector3(p.x, y, p.z);
        }

        // No skeleton → no hand bone; the ball falls back to fixed-offset carry math.
        public Transform GetHandBone() => null;

        // One shared, tinted-per-instance material for every capsule body.
        private static Material _sharedBody;
        private static Material SharedBodyMaterial()
        {
            if (_sharedBody == null) _sharedBody = Match3DMaterials.CreateLit(Color.white);
            return _sharedBody;
        }
    }
}
