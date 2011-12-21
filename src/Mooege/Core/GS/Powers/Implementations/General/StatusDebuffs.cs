﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mooege.Core.GS.Ticker;
using Mooege.Net.GS.Message;
using Mooege.Net.GS.Message.Definitions.Misc;
using Mooege.Core.GS.Players;

namespace Mooege.Core.GS.Powers.Implementations
{
    public class SimpleBooleanStatusDebuff : PowerBuff
    {
        GameAttributeB _statusAttribute;
        GameAttributeB _immuneCheckAttribute;
        FloatingNumberMessage.FloatType? _floatMessage;
        bool _immuneBlocked;

        public SimpleBooleanStatusDebuff(GameAttributeB statusAttribute, GameAttributeB immuneCheckAttribute,
            FloatingNumberMessage.FloatType? floatMessage = null)
        {
            _statusAttribute = statusAttribute;
            _immuneCheckAttribute = immuneCheckAttribute;
            _floatMessage = floatMessage;
            _immuneBlocked = false;
        }

        public override void Init()
        {
            if (_immuneCheckAttribute != null)
                _immuneBlocked = Target.Attributes[_immuneCheckAttribute];
        }

        public override bool Apply()
        {
            if (!base.Apply())
                return false;

            if (_immuneBlocked)
                return false;  // TODO: play immune float message?

            Target.Attributes[_statusAttribute] = true;
            Target.Attributes.BroadcastChangedIfRevealed();

            if (_floatMessage != null)
            {
                if (User is Player)
                {
                    (User as Player).InGameClient.SendMessage(new FloatingNumberMessage
                    {
                        ActorID = this.Target.DynamicID,
                        Type = _floatMessage.Value
                    });
                }
            }

            return true;
        }

        public override void Remove()
        {
            base.Remove();
            Target.Attributes[_statusAttribute] = false;
            Target.Attributes.BroadcastChangedIfRevealed();
        }

        public override bool Stack(Buff buff)
        {
            if (((SimpleBooleanStatusDebuff)buff)._immuneBlocked)
                return true;  // swallow buff if it was blocked

            return base.Stack(buff);
        }
    }

    [ImplementsPowerSNO(103216)] // DebuffBlind.pow
    [ImplementsPowerBuff(0)]
    public class DebuffBlind : SimpleBooleanStatusDebuff
    {
        public DebuffBlind(TickTimer timeout)
            : base(GameAttribute.Blind, GameAttribute.Immune_To_Blind, FloatingNumberMessage.FloatType.Blinded)
        {
            Timeout = timeout;
        }
    }

    [ImplementsPowerSNO(30195)] // DebuffChilled.pow
    [ImplementsPowerBuff(0)]
    public class DebuffChilled : SimpleBooleanStatusDebuff
    {
        public DebuffChilled(TickTimer timeout)
            : base(GameAttribute.Chilled, null, null)
        {
            Timeout = timeout;
        }
    }

    [ImplementsPowerSNO(101000)] // DebuffStunned.pow
    [ImplementsPowerBuff(0)]
    public class DebuffStunned : SimpleBooleanStatusDebuff
    {
        public DebuffStunned(TickTimer timeout)
            : base(GameAttribute.Stunned, GameAttribute.Stun_Immune, FloatingNumberMessage.FloatType.Stunned)
        {
            Timeout = timeout;
        }
    }
}
