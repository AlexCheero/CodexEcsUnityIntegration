using UnityEngine;

public class EntityViewChild : MonoBehaviour
{
    [SerializeField]
    private EntityView _ownerView;

    public EntityView OwnerView => _ownerView;
}
