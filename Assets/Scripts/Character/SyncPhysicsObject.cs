using UnityEngine;

public class SyncPhysicsObject : MonoBehaviour
{
    
    Rigidbody rigidbody3D;

    ConfigurableJoint joint;

    [SerializeField] Rigidbody animatedRigidbody3D;

    [SerializeField] bool syncAnimation = false;

    Quaternion startLocalRotation;

    float startSlerpPositionSpring = 0.0f;
    

    void Awake()
    {
        rigidbody3D = GetComponent<Rigidbody>();

        joint = GetComponent<ConfigurableJoint>();

        startLocalRotation = transform.localRotation;

        startSlerpPositionSpring = joint.slerpDrive.positionSpring;
    }

    public void UpdateJointAnimation()
    {
        if(!syncAnimation)
            return;

        ConfigurableJointExtensions.SetTargetRotationLocal(joint, animatedRigidbody3D.transform.localRotation, startLocalRotation);
    }

    public void MakeRagdoll()
    {
        JointDrive jointDrive = joint.slerpDrive;
        jointDrive.positionSpring = 1f;
        joint.slerpDrive = jointDrive;

    }

    public void MakeActiveRagdoll()
    {
        JointDrive jointDrive = joint.slerpDrive;
        jointDrive.positionSpring = startSlerpPositionSpring;
        joint.slerpDrive = jointDrive;
    }


}
