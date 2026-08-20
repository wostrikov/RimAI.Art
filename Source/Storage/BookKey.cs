/*
 * File: BookKey.cs
 *
 * Purpose:
 * - Provide a stable, save-safe identifier for a specific book instance.
 *
 * Dependencies:
 * - Verse.Thing
 *
 * Design notes:
 * - Should be deterministic across save/load.
 * - Typically derived from ThingID + map or similar stable identifiers.
 *
 * Do NOT:
 * - Do not store large data here.
 */
using System;
using System.Runtime.Serialization;

namespace Ustas.RimAI.Art.Storage
{
    [DataContract]
    public sealed class BookKey : IEquatable<BookKey>
    {
        [DataMember(Name = "id")]
        public string Id { get; private set; }

        public BookKey(string id)
        {
            Id = id ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);

        public override string ToString()
        {
            return Id ?? string.Empty;
        }

        public bool Equals(BookKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is BookKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0;
        }
    }
}
