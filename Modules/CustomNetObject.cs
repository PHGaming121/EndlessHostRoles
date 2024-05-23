using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

// Credit: https://github.com/Rabek009/MoreGamemodes/blob/e054eb498094dfca0a365fc6b6fea8d17f9974d7/Modules/CustomObjects
// Huge thanks to Rabek009 for this code!

namespace EHR
{
    public class CustomNetObject
    {
        private static readonly Dictionary<int, CustomNetObject> AllObjects = [];
        private readonly int Id = Enumerable.Range(0, int.MaxValue).First(id => !AllObjects.ContainsKey(id));
        private PlayerControl PC;

        protected void RpcChangeSprite(string sprite)
        {
            PC.RpcSetName(sprite);
        }

/*
        public void TP(Vector2 position)
        {
            PC.NetTransform.RpcSnapTo(position);
        }
*/

        protected void Despawn()
        {
            PC.Despawn();
            AllObjects.Remove(Id);
        }

        protected void Hide(PlayerControl player)
        {
            if (player.AmOwner)
            {
                PC.Visible = false;
                return;
            }

            MessageWriter writer = MessageWriter.Get();
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(player.GetClientId());
            writer.StartMessage(5);
            writer.WritePacked(PC.NetId);
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }

        protected virtual void OnFixedUpdate()
        {
        }

        protected void CreateNetObject(string sprite, Vector2 position)
        {
            PC = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
            PC.PlayerId = 255;
            PC.isNew = false;
            PC.notRealPlayer = true;
            AmongUsClient.Instance.NetIdCnt += 1U;
            MessageWriter msg = MessageWriter.Get();
            msg.StartMessage(5);
            msg.Write(AmongUsClient.Instance.GameId);
            AmongUsClient.Instance.WriteSpawnMessage(PC, -2, SpawnFlags.None, msg);
            msg.EndMessage();
            msg.StartMessage(6);
            msg.Write(AmongUsClient.Instance.GameId);
            msg.WritePacked(int.MaxValue);
            for (uint i = 1; i <= 3; ++i)
            {
                msg.StartMessage(4);
                msg.WritePacked(2U);
                msg.WritePacked(-2);
                msg.Write((byte)SpawnFlags.None);
                msg.WritePacked(1);
                msg.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
                msg.StartMessage(1);
                msg.EndMessage();
                msg.EndMessage();
            }

            msg.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(msg);
            msg.Recycle();
            if (PlayerControl.AllPlayerControls.Contains(PC))
                PlayerControl.AllPlayerControls.Remove(PC);
            _ = new LateTask(() =>
            {
                PC.RpcSetName(sprite);
                PC.NetTransform.RpcSnapTo(position);
            }, 0.1f);
            PC.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
            PC.cosmetics.colorBlindText.color = Color.clear;
            AllObjects.Add(Id, this);
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc.AmOwner) continue;
                _ = new LateTask(() =>
                {
                    CustomRpcSender sender = CustomRpcSender.Create("SetFakeData");
                    MessageWriter writer = sender.stream;
                    sender.StartMessage(pc.GetClientId());
                    writer.StartMessage(1);
                    {
                        writer.WritePacked(PC.NetId);
                        writer.Write(pc.PlayerId);
                    }
                    writer.EndMessage();
                    sender.StartRpc(PC.NetId, (byte)RpcCalls.MurderPlayer)
                        .WriteNetObject(PC)
                        .Write((int)MurderResultFlags.FailedError)
                        .EndRpc();
                    writer.StartMessage(1);
                    {
                        writer.WritePacked(PC.NetId);
                        writer.Write((byte)255);
                    }
                    writer.EndMessage();
                    sender.EndMessage();
                    sender.SendMessage();
                }, 0.1f);
            }
        }

        public static CustomNetObject Get(int id) => AllObjects.GetValueOrDefault(id);

        public static void FixedUpdate() => AllObjects.Values.Do(x => x.OnFixedUpdate());
    }

    public class Explosion : CustomNetObject
    {
        private readonly float Duration;

        private readonly float Size;
        private int Frame;
        private float Timer;

        public Explosion(float size, float duration, Vector2 position)
        {
            Size = size;
            Duration = duration;
            Timer = -0.1f;
            Frame = 0;
            CreateNetObject($"<size={Size}><line-height=85%><font=\"VCR SDF\"><br><#0000>███<#ff0000>█<#0000>███<br><#ff0000>█<#0000>█<#ff0000>███<#0000>█<#ff0000>█<br>█<#ff8000>██<#ffff00>█<#ff8000>██<#ffff00>█<br>██<#ff8000>█<#ffff00>█<#ff8000>█<#ffff00>██<br><#ff8000>█<#ffff80>██<#ffff00>█<#ffff80>██<#ff8000>█<br><#0000>█<#ff8000>█<#ffff80>███<#ff8000>█<#0000>█<br>██<#ff8000>███<#0000>██", position);
        }

        protected override void OnFixedUpdate()
        {
            Timer += Time.deltaTime;
            if (Timer >= Duration / 5f && Frame == 0)
            {
                RpcChangeSprite($"<size={Size}><line-height=85%><font=\"VCR SDF\"><br><#0000>█<#ff0000>█<#0000>█<#ff0000>█<#0000>█<#ff0000>█<#0000>█<br><#ff0000>█<#ff8000>█<#ff0000>█<#ff8000>█<#ff0000>█<#ff8000>█<#ff0000>█<br><#ff8000>██<#ffff00>█<#ff8000>█<#ffff00>█<#ff8000>██<br><#ffff00>███████<br><#ff8000>█<#ffff00>█████<#ff8000>█<br>██<#ffff00>█<#ff8000>█<#ffff00>█<#ff8000>██<br><#ff0000>█<#0000>█<#ff8000>█<#ff0000>█<#ff8000>█<#0000>█<#ff0000>█");
                Frame = 1;
            }

            if (Timer >= Duration / 5f * 2f && Frame == 1)
            {
                RpcChangeSprite($"<size={Size}><line-height=85%><font=\"VCR SDF\"><br><#0000>█<#c0c0c0>█<#ff0000>█<#000000>█<#ff0000>█<#c0c0c0>█<#0000>█<br><#c0c0c0>█<#808080>█<#ff0000>█<#ff8000>█<#ff0000>█<#c0c0c0>██<br><#ff0000>██<#ff8000>█<#ffff00>█<#ff8000>█<#ff0000>██<br><#c0c0c0>█<#ff8000>█<#ffff00>█<#ffff80>█<#ffff00>█<#ff8000>█<#808080>█<br><#ff0000>██<#ff8000>█<#ffff00>█<#ff8000>█<#ff0000>██<br><#c0c0c0>█<#808080>█<#ff0000>█<#ff8000>█<#ff0000>█<#000000>█<#c0c0c0>█<br><#0000>█<#c0c0c0>█<#ff0000>█<#c0c0c0>█<#ff0000>█<#c0c0c0>█<#0000>█");
                Frame = 2;
            }

            if (Timer >= Duration / 5f * 3f && Frame == 2)
            {
                RpcChangeSprite($"<size={Size}><line-height=85%><font=\"VCR SDF\"><br><#ff0000>█<#ff8000>█<#0000>█<#808080>█<#0000>█<#ff8000>█<#ff0000>█<br><#ff8000>█<#0000>█<#ffff00>█<#c0c0c0>█<#ffff00>█<#0000>█<#ff8000>█<br><#0000>█<#ffff00>█<#c0c0c0>███<#ffff00>█<#0000>█<br><#808080>█<#c0c0c0>█████<#808080>█<br><#0000>█<#ffff00>█<#c0c0c0>███<#ffff00>█<#0000>█<br><#ff8000>█<#0000>█<#ffff00>█<#c0c0c0>█<#ffff00>█<#0000>█<#ff8000>█<br><#ff0000>█<#ff8000>█<#0000>█<#808080>█<#0000>█<#ff8000>█<#ff0000>█");
                Frame = 3;
            }

            if (Timer >= Duration / 5f * 4f && Frame == 3)
            {
                RpcChangeSprite($"<size={Size}><line-height=85%><font=\"VCR SDF\"><br><#0000>█<#808080>█<#0000>██<#c0c0c0>█<#0000>█<#808080>█<br><#ffff00>█<#0000>██<#c0c0c0>█<#0000>█<#808080>█<#0000>█<br>█<#808080>█<#c0c0c0>████<#0000>█<br>█<#c0c0c0>██████<br>█<#0000>█<#c0c0c0>███<#808080>█<#0000>█<br>█<#c0c0c0>█<#0000>█<#c0c0c0>█<#0000>█<#c0c0c0>██<br><#808080>█<#0000>█<#c0c0c0>█<#0000>█<#808080>█<#0000>█<#ffff00>█");
                Frame = 4;
            }

            if (Timer >= Duration && Frame == 4)
            {
                Despawn();
            }
        }
    }

    public class TrapArea : CustomNetObject
    {
        private readonly float Size;
        private readonly float WaitDuration;
        private int State;
        private float Timer;

        public TrapArea(float radius, float waitDuration, Vector2 position, List<byte> visibleList)
        {
            Size = radius * 25f;
            Timer = -0.1f;
            WaitDuration = waitDuration;
            State = 0;
            CreateNetObject($"<size={Size}><font=\"VCR SDF\"><#c7c7c769>●", position);
            foreach (var pc in Main.AllPlayerControls)
            {
                if (!visibleList.Contains(pc.PlayerId))
                    Hide(pc);
            }
        }

        protected override void OnFixedUpdate()
        {
            Timer += Time.deltaTime;
            if (Timer >= WaitDuration * 0.75f && State == 0)
            {
                RpcChangeSprite($"<size={Size}><font=\"VCR SDF\"><#fff70069>●");
                State = 1;
            }

            if (Timer >= WaitDuration * 0.95f && State == 1)
            {
                RpcChangeSprite($"<size={Size}><font=\"VCR SDF\"><#ff000069>●");
                State = 2;
            }
        }
    }
}