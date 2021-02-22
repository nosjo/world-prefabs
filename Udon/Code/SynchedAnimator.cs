
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class SynchedAnimator : UdonSharpBehaviour
{
    [SerializeField] private Animator anim;
    private bool running = false;
    private int nextsync = 0;
    private int last_zero_local = -1;
    [UdonSynced] private int last_zero = 0;
    [Header("Alternate sync mode might work better but requires more data sync (not recommended)")]
    [SerializeField] private bool alternatemode;
    private float animlen;

    void Start()
    {
        if (!anim)
        {
            anim = gameObject.GetComponent<Animator>();
            Debug.Log("Unsupported use case cant move/rotate object with animator when script is attached to same object");
        }
        animlen = anim.GetCurrentAnimatorClipInfo(0)[0].clip.length;
    }

    public override void OnOwnershipTransferred()
    {
        if (alternatemode && Networking.IsOwner(gameObject) && Networking.GetServerTimeInMilliseconds() + (int)(anim.GetCurrentAnimatorClipInfo(0)[0].clip.length * 1000) - 1000 > last_zero)
        {
            last_zero = 0;
        }
    }

    public override void OnDeserialization()
    {
        bool master = Networking.IsOwner(gameObject);
        if (!master && last_zero_local != last_zero)
        {
            last_zero_local = last_zero;
            running = false;
            nextsync = 0;
        }
    }

    void FixedUpdate()
    {
        bool master = Networking.IsOwner(gameObject);
        int time = Networking.GetServerTimeInMilliseconds();
        if (!master && 0 == last_zero)
        {
            return;
        }
        else if (alternatemode)
        {
            if (master && (last_zero_local == -1 || time >= last_zero + 1000))
            {
                AnimatorClipInfo[] state = anim.GetCurrentAnimatorClipInfo(0);
                last_zero_local = time + (int)(state[0].clip.length * 1000) - 1000;
                last_zero = last_zero_local;
                running = false;
            }
            else if (!running && time >= last_zero_local)
            {
                anim.Rebind();
                running = true;
            }
        }
        else
        {
            if (master && last_zero_local == -1)
            {
                last_zero_local = time;
                last_zero = time;
            }
            else if (nextsync == 0)
            {
                nextsync = last_zero;
                while (nextsync < time)
                {
                    nextsync += (int)(animlen * 1000);
                }
            }
            else if (time >= nextsync)
            {
                nextsync += (int)(animlen * 1000);
                anim.Rebind();
            }
        }
    }
}
