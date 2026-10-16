using UnityEngine;

public class SyncPhysicsObject : MonoBehaviour
{

    Rigidbody rigidbody3D;

    ConfigurableJoint joint;

    [SerializeField] Rigidbody animatedRigidbody3D;

    [SerializeField] bool syncAnimation = false;

    Quaternion startLocalRotation;

    float startSlerpPositionSpring = 0.0f;

    //Original drive and angular speed limit, restored when a swing ends
    JointDrive startDrive;
    float startMaxAngularVelocity = 0.0f;

    //Swing override, used to drive an arm by code instead of by the animation
    bool isSwinging = false;
    Quaternion swingLocalRotation;


    void Awake()
    {
        rigidbody3D = GetComponent<Rigidbody>();

        joint = GetComponent<ConfigurableJoint>();

        startLocalRotation = transform.localRotation;

        startSlerpPositionSpring = joint.slerpDrive.positionSpring;

        startDrive = joint.slerpDrive;
        startMaxAngularVelocity = rigidbody3D.maxAngularVelocity;
    }

    public void UpdateJointAnimation()
    {
        if(isSwinging)
        {
            ConfigurableJointExtensions.SetTargetRotationLocal(joint, swingLocalRotation, startLocalRotation);
            return;
        }

        if(!syncAnimation)
            return;

        ConfigurableJointExtensions.SetTargetRotationLocal(joint, animatedRigidbody3D.transform.localRotation, startLocalRotation);
    }

    //Takes over the joint with a strong drive so the limb can swing fast like a lever
    public void BeginSwing(float spring, float damper, float maxAngularVelocity)
    {
        isSwinging = true;

        JointDrive jointDrive = joint.slerpDrive;
        jointDrive.positionSpring = spring;
        jointDrive.positionDamper = damper;
        jointDrive.useAcceleration = true;
        joint.slerpDrive = jointDrive;

        rigidbody3D.maxAngularVelocity = maxAngularVelocity;
    }

    //Rotates the animated pose of this limb around a world axis
    public void SetSwingAngle(Vector3 worldAxis, float angle)
    {
        Transform animatedTransform = animatedRigidbody3D.transform;

        Vector3 localAxis = animatedTransform.parent.InverseTransformDirection(worldAxis);

        swingLocalRotation = Quaternion.AngleAxis(angle, localAxis) * animatedTransform.localRotation;
    }

    //Gives the limb back to the animation and relaxes it to its normal floppy drive
    public void EndSwing()
    {
        if(!isSwinging)
            return;

        isSwinging = false;

        joint.slerpDrive = startDrive;
        rigidbody3D.maxAngularVelocity = startMaxAngularVelocity;
    }

    public void MakeRagdoll()
    {
        EndSwing();

        JointDrive jointDrive = joint.slerpDrive;
        jointDrive.positionSpring = 1f;
        joint.slerpDrive = jointDrive;

    }

    public void MakeActiveRagdoll()
    {
        EndSwing();

        JointDrive jointDrive = joint.slerpDrive;
        jointDrive.positionSpring = startSlerpPositionSpring;
        joint.slerpDrive = jointDrive;
    }


}
