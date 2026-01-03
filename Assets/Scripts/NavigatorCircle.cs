using UnityEngine;

namespace TopDownRace
{
    public class NavigatorCircle : MonoBehaviour
    {
        private CarNavigator navigator;

        void Start()
        {
            navigator = GetComponentInParent<CarNavigator>();
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("Rival") || other.CompareTag("Wall"))
            {
                navigator.AvoidObstacle(other);
            }
        }
    }
}
