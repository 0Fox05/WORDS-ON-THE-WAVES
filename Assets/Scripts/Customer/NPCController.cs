using UnityEngine;
using UnityEngine.AI;

public class NPCController : MonoBehaviour
{
    public Animator animator;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // Update animation speed based on agent velocity
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }
}
