using System.Collections;
using UnityEngine;

namespace Mirror
{
    // [RequireComponent(typeof(Rigidbody))] <- OnValidate ensures this is on .target
    [AddComponentMenu("Network/Network Rigidbody (Unreliable)")]
    public class NetworkRigidbodyUnreliable : NetworkTransformUnreliable
    {
        [SerializeField] bool _optimizeBandwidth;
        bool _wasSyncOnChange;
        bool _wasChangeDet;
        bool clientAuthority => syncDirection == SyncDirection.ClientToServer;

        Rigidbody rb;
        bool wasKinematic;

        protected override void OnValidate()
        {
            // Skip if Editor is in Play mode
            if (Application.isPlaying) return;

            base.OnValidate();

            // we can't overwrite .target to be a Rigidbody.
            // but we can ensure that .target has a Rigidbody, and use it.
            if (target.GetComponent<Rigidbody>() == null)
            {
                Debug.LogWarning($"{name}'s NetworkRigidbody.target {target.name} is missing a Rigidbody", this);
            }
        }

        // cach Rigidbody and original isKinematic setting
        protected override void Awake()
        {
            // we can't overwrite .target to be a Rigidbody.
            // but we can use its Rigidbody component.
            rb = target.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError($"{name}'s NetworkRigidbody.target {target.name} is missing a Rigidbody", this);
                return;
            }
            _wasSyncOnChange = onlySyncOnChange;
            _wasChangeDet = changedDetection;
            wasKinematic = rb.isKinematic;
            base.Awake();
        }

        // reset forced isKinematic flag to original.
        // otherwise the overwritten value would remain between sessions forever.
        // for example, a game may run as client, set rigidbody.iskinematic=true,
        // then run as server, where .iskinematic isn't touched and remains at
        // the overwritten=true, even though the user set it to false originally.
        public override void OnStopServer() => rb.isKinematic = wasKinematic;
        public override void OnStopClient() => rb.isKinematic = wasKinematic;

        public override void OnStartAuthority()
        {
            rb.isKinematic = wasKinematic;
            base.OnStartAuthority();
        }

        // overwriting Construct() and Apply() to set Rigidbody.MovePosition
        // would give more jittery movement.

        // FixedUpdate for physics
        void FixedUpdate()
        {
            // who ever has authority moves the Rigidbody with physics.
            // everyone else simply sets it to kinematic.
            // so that only the Transform component is synced.

            // host mode
            if (isServer && isClient)
            {
                // in host mode, we own it it if:
                // clientAuthority is disabled (hence server / we own it)
                // clientAuthority is enabled and we have authority over this object.
                bool owned = !clientAuthority || IsClientWithAuthority || netIdentity.requestingClientAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!owned) rb.isKinematic = true;
            }
            // client only
            else if (isClient)
            {
                // on the client, we own it only if clientAuthority is enabled,
                // and we have authority over this object.
                bool owned = IsClientWithAuthority || netIdentity.requestingClientAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!owned) rb.isKinematic = true;
            }
            // server only
            else if (isServer)
            {
                // on the server, we always own it if clientAuthority is disabled.
                bool owned = !clientAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!owned) rb.isKinematic = true;
            }
            if (_optimizeBandwidth)
            {
                if (isServer && (onlySyncOnChange != _wasSyncOnChange || changedDetection != _wasChangeDet) && !rb.isKinematic && rb.IsSleeping() && authority)
                {
                    StartCoroutine(WaitBeforeReturn());
                }
                else if (!isServer && authority && !rb.isKinematic && !rb.IsSleeping() && (onlySyncOnChange || changedDetection))
                {
                    onlySyncOnChange = false;
                    changedDetection = false;
                    CmdUpdateSettings(onlySyncOnChange, changedDetection);
                }
            }
        }

        IEnumerator WaitBeforeReturn()
        {
            yield return new WaitForSecondsRealtime(0.05f);
            if (isServer && (onlySyncOnChange != _wasSyncOnChange || changedDetection != _wasChangeDet) && !rb.isKinematic && rb.IsSleeping() && authority)
            {
                onlySyncOnChange = _wasSyncOnChange;
                changedDetection = _wasChangeDet;
                RpcUpdateSettings(onlySyncOnChange, changedDetection);
            }
        }

        [Command]
        public void CmdUpdateSettings(bool syncOnChange, bool changedDet)
        {
            onlySyncOnChange = syncOnChange;
            changedDetection = changedDet;
            RpcUpdateSettings(onlySyncOnChange, changedDetection);
        }

        [ClientRpc]
        public void RpcUpdateSettings(bool syncOnChange, bool changedDet)
        {
            onlySyncOnChange = syncOnChange;
            changedDetection = changedDet;
        }

        protected override void OnTeleport(Vector3 destination)
        {
            base.OnTeleport(destination);

            rb.position = transform.position;
        }

        protected override void OnTeleport(Vector3 destination, Quaternion rotation)
        {
            base.OnTeleport(destination, rotation);

            rb.position = transform.position;
            rb.rotation = transform.rotation;
        }
    }
}
