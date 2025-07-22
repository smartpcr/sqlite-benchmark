// -----------------------------------------------------------------------
// <copyright file="DeliveryType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace SQLite.Lib.Models
{
    /// <summary>
    /// Represents the delivery type of an update.
    /// </summary>
    public enum DeliveryType
    {
        /// <summary>
        /// Update is delivered automatically.
        /// </summary>
        Automatic = 0,

        /// <summary>
        /// Update requires manual delivery.
        /// </summary>
        Manual = 1,

        /// <summary>
        /// Update is delivered on-demand.
        /// </summary>
        OnDemand = 2
    }
}