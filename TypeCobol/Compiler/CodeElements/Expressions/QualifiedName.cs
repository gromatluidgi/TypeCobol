﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace TypeCobol.Compiler.CodeElements.Expressions
{

    public interface QualifiedName : IList<string>
    {
        char Separator { get; }
        string Head { get; }
        string Tail { get; }
        QualifiedName Parent { get; }
        bool IsExplicit { get; }
        bool Matches(string uri);
        bool Matches(QualifiedName name);
    }



    public abstract class AbstractQualifiedName : QualifiedName, IEquatable<AbstractQualifiedName>
    {
        public virtual bool IsExplicit { get { return false; } }
        public virtual char Separator
        {
            get { return '.'; }
            set { throw new System.NotSupportedException(); }
        }

        public abstract string Head { get; }
        public virtual string Tail { get; }

        
        public abstract QualifiedName Parent { get; }
        public abstract int Count { get; }
        public abstract IEnumerator<string> GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public bool IsReadOnly { get { return true; } }
        public void Add(string item) { throw new System.NotSupportedException(); }
        public bool Remove(string item) { throw new System.NotSupportedException(); }
        public void Clear() { throw new System.NotSupportedException(); }
        public bool Contains(string item)
        {
            foreach (string name in this)
                if (name.Equals(item)) return true;
            return false;
        }
        public void CopyTo(string[] array, int index)
        {
            if (array == null) throw new System.ArgumentNullException();
            if (index < 0) throw new System.ArgumentOutOfRangeException();
            if (array.Length < index + Count) throw new System.ArgumentException();
            int c = 0;
            foreach (string name in this)
            {
                array[index + c] = name;
                c++;
            }
        }
        public string this[int index]
        {
            get
            {
                int c = 0;
                foreach (string name in this)
                    if (c == index) return name;
                    else c++;
                throw new System.ArgumentOutOfRangeException(index + " outside of [0," + Count + "[");
            }
            set { throw new System.NotSupportedException(); }
        }
        public int IndexOf(string item)
        {
            int c = 0;
            foreach (string name in this)
                if (name.Equals(item)) return c;
                else c++;
            return -1;
        }
        public void Insert(int index, string item) { throw new System.NotSupportedException(); }
        public void RemoveAt(int index) { throw new System.NotSupportedException(); }

        public override bool Equals(System.Object other)
        {
            return Equals(other as AbstractQualifiedName);
        }
        public virtual bool Equals(AbstractQualifiedName abstractQualifiedName)
        {
            if (Object.ReferenceEquals(this, abstractQualifiedName)) return true;
            if (Object.ReferenceEquals(null, abstractQualifiedName)) return false;

            if (abstractQualifiedName.Count != Count) return false;
            for (int c = 0; c < Count; c++)
                if (!abstractQualifiedName[c].Equals(this[c])) return false;
            return true;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 13;
                hash = (hash * 397) ^ Count;
                foreach (string part in this)
                    hash = (hash * 397) ^ part.GetHashCode();
                return hash;
            }
        }

        public bool Matches(string uri)
        {
            return this.ToString().EndsWith(uri);
        }
        public bool Matches(QualifiedName name)
        {
            return this.Matches(name.ToString());
        }
    }



    public class URI : AbstractQualifiedName
    {
        public string Value { get { return this.ToString(); } }
        private IEnumerable<string> parts;

        public URI(IEnumerable<string> UriParts, char separator = '.')
        {
            parts = UriParts;
            _separator = separator;
        }

        public URI(string uri, char separator = '.')
        {
            if (uri == null) throw new System.ArgumentNullException("URI must not be null.");
            _separator = separator;
            parts = uri.Split(this.Separator);
        }

        private char _separator;
        public override char Separator
        {
            get { return _separator; }
            set { _separator = value; }
        }

        public override string ToString() { return string.Join(_separator.ToString(), parts); }

        public override string Tail {get { return parts.First(); }}

        public override string Head { get { return parts.Last(); } }
        public override QualifiedName Parent
        {
            get
            {
                if (parts.Count() > 1)
                    return new URI(parts.Take(parts.Count() - 1));
                else
                    return null;
            }
        }

        public override IEnumerator<string> GetEnumerator()
        {
            foreach (string part in parts) yield return part;
        }


        public override bool IsExplicit { get { return false; } }

        public override int Count { get { return parts.Count(); } }
    }
}
