// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace LuisPredict.Sdk
{
    /// <summary>
    /// Specifies the slot that contains the published model.
    /// </summary>
    public enum PublishSlot
    {
        /// <summary>
        /// Model is in the staging slot.
        /// </summary>
        Staging,

        /// <summary>
        /// Model is in the production slot.
        /// </summary>
        Production
    }
}
