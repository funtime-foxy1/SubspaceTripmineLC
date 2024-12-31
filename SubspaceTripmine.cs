using GameNetcodeStuff;
using LethalLib.Modules;
using LethalNetworkAPI;
using LethalNetworkAPI.Serializable;
using LethalNetworkAPI.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace SubspaceTripmineLC
{
    class SubspaceTripmine : PhysicsProp
    {
        public LNetworkVariable<bool> isPlanted;
        //public static LNetworkVariable<float> colora;

        Coroutine fadeOut;

        public LNetworkMessage<NetworkObjectReference> explode;
        public LNetworkEvent plant;
        public LNetworkEvent unplant;
        public LNetworkMessage<int> plsDie;
        /// <summary>
        /// Puts the subspace on the floor and makes it transparent. Can be disarmed by picking it up.
        /// </summary>
        public void Plant(bool changeVar = true)
        {
            STPlugin.Log.LogInfo("Subspace Tripmine has been planted.");
            isPlanted.Value = true;
        }

        /// <summary>
        /// Deactivate the mine and be ready for grabbing
        /// </summary>
        public void Unplant(bool changeVar = true)
        {
            STPlugin.Log.LogInfo("Subspace Tripmine has been unplanted.");
            isPlanted.Value = false;
        }

        public override void Start()
        {
            base.Start();

            var id = this.NetworkObject.NetworkObjectId;
            STPlugin.Log.LogInfo("Initalizing networks for Subspace #" + id);
            explode = LNetworkMessage<NetworkObjectReference>.Connect("explode" + id);
            isPlanted = LNetworkVariable<bool>.Connect("isPlanted" + id, default, LNetworkVariableWritePerms.Server);
            plsDie = LNetworkMessage<int>.Connect("plsdie" + id);
            //SubspaceTripmine.colora = LNetworkVariable<float>.Connect("colora" + id, default, LNetworkVariableWritePerms.Server);
            plant = LNetworkEvent.Connect("plant" + id);
            unplant = LNetworkEvent.Connect("unplant" + id);

            plant.OnServerReceived += (_id) =>
            {
                Plant();
            };
            unplant.OnServerReceived += (_id) =>
            {
                Unplant();
            };
            explode.OnClientReceived += (_obj) =>
            {
                var obj = GetNetworkObject(_obj.NetworkObjectId);
                //Explode(GetNetworkObject(obj.NetworkObjectId));
                var player = obj.gameObject.GetComponent<PlayerControllerB>();
                var light = transform.Find("ExplosionLight").gameObject.GetComponent<Light>();
                if (light == null)
                {
                    STPlugin.Log.LogInfo("why.");
                }
                STPlugin.Log.LogDebug(player.playerUsername);
                if (player.NetworkObject.NetworkObjectId == _obj.NetworkObjectId)
                {
                    if (STPlugin.instantlyKillPlayer.Value)
                    {
                        player.KillPlayer(Vector3.up * 100, true, CauseOfDeath.Blast);
                    }
                    else
                    {
                        player.DamagePlayer(STPlugin.damageDone.Value, true, true, CauseOfDeath.Blast);
                    }
                }
                var explosionSound = STPlugin.assets.LoadAsset<AudioClip>("subspace-tripmine-explosion");
                light.gameObject.SetActive(true);
                light.intensity = 600;
                if (STPlugin.enableExplosion.Value)
                {
                    gameObject.GetComponent<AudioSource>().clip = explosionSound;
                    gameObject.GetComponent<AudioSource>().Play();
                }
                StartCoroutine("FadeOutLight", light);
                if (STPlugin.destroyAfterExplosion.Value)
                {
                    transform.Find("InnerStar").gameObject.SetActive(false);
                    transform.Find("OuterBlock").gameObject.SetActive(false);
                    transform.Find("Trigger").gameObject.SetActive(false);
                    Destroy(gameObject.GetComponent<BoxCollider>());
                }
            };
            plsDie.OnClientReceived += (damage) =>
            {
                var player = StartOfRound.Instance.localPlayerController;
                if (damage >= 100)
                {
                    return;
                }
                
            };
            //colora.OnValueChanged += (prev, newval) =>
            //{
            //    if (gameObject is null || !(gameObject is GameObject))
            //    {
            //        STPlugin.Log.LogDebug("gameObject is bugged, saving a NRE.");
            //        return;
            //    }
            //    if (gameObject == null)
            //    {
            //        STPlugin.Log.LogDebug("GameObject is null :[");
            //        return;
            //    }
            //    if (gameObject.GetComponentsInChildren<MeshRenderer>().Length < 1)
            //    {
            //        STPlugin.Log.LogDebug("0 MeshRenderers");
            //        return;
            //    }
            //};
            isPlanted.Value = false;

            isPlanted.OnValueChanged += (prev, newval) =>
            {
                STPlugin.Log.LogDebug("isPlanted changed for subspace #" + id);
                if (newval)
                {
                    fadeOut = StartCoroutine("FadeOutObject", gameObject);

                }
                else
                {
                    if (fadeOut is null || !(fadeOut is Coroutine)) {
                        STPlugin.Log.LogDebug("fadeOut is bugged, saving a NRE.");
                        return;
                    }
                    if (fadeOut != null) StopCoroutine(fadeOut);
                    var mr1 = gameObject.GetComponentsInChildren<MeshRenderer>()[0];
                    var mr2 = gameObject.GetComponentsInChildren<MeshRenderer>()[1];
                    var newColor1 = mr1.materials[0].color;
                    var newColor2 = mr2.materials[0].color;
                    newColor1.a = 1;
                    newColor2.a = 1;
                    mr1.materials[0].color = newColor1;
                    mr2.materials[0].color = newColor2;
                }
            };
            plant.InvokeServer();


            GetComponentInChildren<Rigidbody>().gameObject.AddComponent<GrabbableObjectPhysicsTrigger>();
            GetComponentInChildren<GrabbableObjectPhysicsTrigger>().itemScript = this;

        }
        public override void ActivatePhysicsTrigger(Collider other)
        {
            //if (IsServer) return;
            base.ActivatePhysicsTrigger(other);
            STPlugin.Log.LogInfo(other);
            if (other.tag == "Player" && isPlanted.Value && StartOfRound.Instance.shipHasLanded)
            {
                var activator = other.gameObject.GetComponentInParent<PlayerControllerB>();
                var reference = new NetworkObjectReference(activator.NetworkObject);
                explode.SendClients(reference);
            }
            
        }

        IEnumerator FadeOutLight(Light light)
        {
            for (float i = 0f; i < 1f; i += Time.deltaTime / 100f)
            {
                light.intensity = Mathf.Lerp(light.intensity, 0, i);
                yield return null;
            }
            STPlugin.Log.LogInfo("Finished fading");
        }

        IEnumerator FadeOutObject(GameObject objToFade)
        {
            var mr1 = gameObject.GetComponentsInChildren<MeshRenderer>()[0];
            var mr2 = gameObject.GetComponentsInChildren<MeshRenderer>()[1];
            var newColor1 = mr1.materials[0].color;
            var newColor2 = mr2.materials[0].color;
            for (float i = 0f; i < 1f; i += Time.deltaTime / 75f)
            {
                newColor1.a = Mathf.Lerp(newColor1.a, STPlugin.plantedTransparency.Value, i);
                newColor2.a = Mathf.Lerp(newColor2.a, STPlugin.plantedTransparency.Value, i);
                mr1.materials[0].color = newColor1;
                mr2.materials[0].color = newColor2;
                yield return null;
            }
        }

        //bool hasCalledGrabThisFrame = false;
        //bool hasCalledReleaseThisFrame = false;
        //bool hasHeldBefore = false;

        //private void Update()
        //{
        //    if (!IsClient) return;
        //    if (this.isHeld && hasCalledGrabThisFrame == false)
        //    {
        //        unplant.InvokeServer();
        //        hasCalledGrabThisFrame = true;
        //        hasCalledReleaseThisFrame = false;
        //        hasHeldBefore = true;
        //    }
        //    if (!this.isHeld && hasHeldBefore && hasCalledReleaseThisFrame == false)
        //    {
        //        plant.InvokeServer();
        //        hasCalledReleaseThisFrame = true;
        //        hasCalledGrabThisFrame = false;
        //    }
        //}

        public override void GrabItem()
        {
            if (!IsClient) return;
            if (unplant == null)
            {
                STPlugin.Log.LogError("Plant is null. uh oh :[");
                return;
            }
            unplant.InvokeServer();
            base.GrabItem();

            //hasCalledGrabThisFrame = true;
            //hasCalledReleaseThisFrame = false;
            //hasHeldBefore = true;
        }

        public override void DiscardItem()
        {
            if (!IsClient) return;
            if (plant == null)
            {
                STPlugin.Log.LogError("Plant is null. uh oh :[");
                return;
            }
            plant.InvokeServer();
            base.DiscardItem();

            //hasCalledReleaseThisFrame = true;
            //hasCalledGrabThisFrame = false;
        }
    }
}
