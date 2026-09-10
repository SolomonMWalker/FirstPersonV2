using Godot;


[GlobalClass]
public partial class RevolverCylinderRotationModifier : SkeletonModifier3D
{
    [Export] public string BoneName { get; set; } = "revolverCylinderRotationBone";

    private int _boneIndex = -1;
    private Quaternion _restRotation = Quaternion.Identity;
    private float _rotationInDegrees;
    private Tween _rotationTween;

    public override void _Ready()
    {
        base._Ready();
        Skeleton3D skeleton = GetSkeleton();
        if (skeleton is null)
        {
            GD.PushError($"{Name} must be a child of a Skeleton3D.");
            return;
        }

        _boneIndex = skeleton.FindBone(BoneName);
        if (_boneIndex < 0)
        {
            GD.PushError($"{Name}: no bone named '{BoneName}' in {skeleton.Name}.");
            return;
        }

        _restRotation = skeleton.GetBoneRest(_boneIndex).Basis.GetRotationQuaternion();
    }
    
    public void RotateTo(float targetRotationInDegrees, float durationInSeconds)
    {
        _rotationTween?.Kill();

        float from = _rotationInDegrees;
        float to = from + Mathf.RadToDeg(Mathf.AngleDifference(
            Mathf.DegToRad(from), Mathf.DegToRad(targetRotationInDegrees)));

        if (durationInSeconds <= 0f)
        {
            ApplyRotation(to);
            return;
        }

        _rotationTween = CreateTween();
        _rotationTween
            .TweenMethod(Callable.From<float>(ApplyRotation), from, to, durationInSeconds)
            .SetTrans(Tween.TransitionType.Expo);
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        if (_boneIndex < 0) return;

        // ponytail: composited from the rest pose because no action keys this bone (only its
        // parent). If one ever does, read GetBonePoseRotation into a cached base instead.
        Quaternion spin = new Quaternion(Vector3.Up, Mathf.DegToRad(_rotationInDegrees));
        GetSkeleton().SetBonePoseRotation(_boneIndex, _restRotation * spin);
    }

    private void ApplyRotation(float rotationInDegrees)
    {
        _rotationInDegrees = Mathf.Wrap(rotationInDegrees, 0f, 360f);
    }
}
