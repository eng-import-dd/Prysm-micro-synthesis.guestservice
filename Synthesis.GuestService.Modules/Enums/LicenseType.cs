﻿namespace Synthesis.GuestService
{
    public enum LicenseType
    {
        /// <summary>
        ///     Default will be used to get the next available license from the default ordered list.
        /// </summary>
        Default,

        /// <summary>
        ///     Standard user license.
        /// </summary>
        UserLicense,

        /// <summary>
        ///     Legacy user perpetual license.
        /// </summary>
        LegacyLicense,

        /// <summary>
        ///     Trial Account License
        /// </summary>
        TrialLicense,

        /// <summary>
        ///     Guest account license.
        /// </summary>
        GuestLicense,

        /// <summary>
        ///     On Prem VM Server License
        /// </summary>
        OnPremLicense
    }
}