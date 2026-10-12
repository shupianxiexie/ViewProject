using UnityEngine;

public class InteractableChecker : MonoBehaviour
{
    public float interactRange = 2f;
    public InteractableItem currentItem;

    void Update()
    {
        CheckInteraction();

        if (Input.GetKeyDown(KeyCode.R))
        {
            Interact();
        }
    }

    void CheckInteraction()
    {
        Collider[] items = Physics.OverlapSphere(transform.position, interactRange);
        currentItem = null;

        float closestDistance = float.MaxValue;

        foreach (Collider item in items)
        {
            InteractableItem interactable = item.GetComponent<InteractableItem>();
            if (interactable == null)
            {
                continue;
            }

            Box box = interactable as Box;
            if (box != null && box.IsHeld)
            {
                continue;
            }

            CarryLight carryLight = interactable as CarryLight;
            if (carryLight != null && carryLight.IsHeld)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, item.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                currentItem = interactable;
            }
        }
    }

    public void Interact()
    {
        Box heldBox = Box.HeldBox;
        if (heldBox != null)
        {
            heldBox.Interact();
            return;
        }

        CarryLight heldLight = CarryLight.HeldLight;
        if (heldLight != null)
        {
            heldLight.Interact();
            return;
        }

        if (currentItem != null)
        {
            currentItem.Interact();
        }
    }
}
