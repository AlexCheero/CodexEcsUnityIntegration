using UnityEngine;

public class EntityViewChild : MonoBehaviour
{
    [SerializeField]
    private EntityView _ownerView;

    public EntityView OwnerView => _ownerView;

    [SerializeField]
    private EntityView_ _ownerView_;

    public EntityView_ OwnerView_ => _ownerView_;
}
