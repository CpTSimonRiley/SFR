﻿using System;
using System.Runtime.CompilerServices;
using SFDGameScriptInterface;

namespace SFR.Fighter;

/// <summary>
///     This class is used to extend the Player modifiers and save new data to them.
///     Basically clones the base class and appends it as a modifier extension to them.
/// </summary>
internal class ExtendedModifiers : IEquatable<ExtendedModifiers>
{
    internal static readonly ConditionalWeakTable<PlayerModifiers, ExtendedModifiers> ExtendedModifiersTable = new();
    internal readonly PlayerModifiers Modifiers;
    public float BulletDodgeChance;
    public float JumpHeightModifier;

    public ExtendedModifiers(PlayerModifiers modifiers)
    {
        Modifiers = modifiers;
        JumpHeightModifier = -1;
        BulletDodgeChance = -1;
    }

    public bool Equals(ExtendedModifiers other) => other != null && other.Modifiers.Equals(Modifiers) && other.JumpHeightModifier == JumpHeightModifier && other.BulletDodgeChance == BulletDodgeChance;
}