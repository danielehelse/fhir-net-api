﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hl7.Fhir.Introspection.Source;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;

namespace Hl7.Fhir.Introspection
{
    public class ElementNavigator : IElementNavigation
    {
        public ElementNavigator(Profile.ProfileStructureComponent structure) : this(structure.Element)
        {
        }

        private ElementNavigator(IList<Profile.ElementComponent> elements)
        {
            if (elements == null) throw Error.ArgumentNull("elements");

            _elements = elements.ToList();      // make a copy of the *list of* elements
            OrdinalPosition = null;
        }

        public ElementNavigator(ElementNavigator other) : this(other._elements)
        {
            ReturnToBookmark(other.Bookmark());
        }

        internal int? OrdinalPosition { get; private set;  }

        private IList<Profile.ElementComponent> _elements;


        public virtual Profile.ElementComponent Current
        {
            get { return OrdinalPosition != null ? _elements[OrdinalPosition.Value] : null; }
        }

        public int Count
        {
            get { return _elements.Count; }
        }


//----------------------------------
//
// Basic relative movement methods
// 
//----------------------------------

        public bool MoveToNext()
        {
            if (OrdinalPosition == null) return false;

            var searchPos = positionAfter();

            // Check whether found an element that is the next non-child element AND has the same parent
            // (which is then our sibling)
            if (searchPos < Count)
            {
                var searchPath = _elements[searchPos].Path;

                if (IsDirectChildPath(this.CurrentParentPath(),searchPath))
                {
                    OrdinalPosition = searchPos;
                    return true;
                }
            }

            return false;
        }

        private int positionAfter()
        {
            var searchPos = OrdinalPosition.Value + 1;

            // Skip children of the element
            while (searchPos < Count && IsDeeperPath(this.CurrentPath(), _elements[searchPos].Path))
                searchPos++;
            return searchPos;
        }
     

        public bool MoveToPrevious()
        {
            if (OrdinalPosition == null) return false;

            var searchPos = positionBefore();


            // Check whether found an element that is the next non-child element AND has the same parent
            // (which is then our sibling)
            if (searchPos >= 0)
            {
                var searchPath = _elements[searchPos].Path;

                if (IsDirectChildPath(this.CurrentParentPath(), searchPath))
                {
                    OrdinalPosition = searchPos;
                    return true;
                }
            }

            return false;
        }

        private int positionBefore()
        {
            var searchPos = OrdinalPosition.Value - 1;

            // Skip children of the previous sibling (if any)
            while (searchPos >= 0 && IsDeeperPath(this.CurrentPath(), _elements[searchPos].Path))
                searchPos--;
            return searchPos;
        }


        public virtual bool MoveToFirstChild()
        {
            if (OrdinalPosition == null) // "root"
            {
                if (Count > 0)
                {
                    OrdinalPosition = 0;
                    return true;
                }
                else
                    return false;
            }
            else
            {
                var childPos = OrdinalPosition.Value + 1;

                if (childPos < Count && IsDirectChildPath(this.CurrentPath(), _elements[childPos].Path))
                {
                    OrdinalPosition = childPos;
                    return true;
                }

                return false;
            }
        }


        public virtual bool MoveToParent()
        {
            if (OrdinalPosition == null)
                return false;
            else if (OrdinalPosition == 0)
            {
                OrdinalPosition = null; // move to "root"
                return true;
            }
            else
            {
                var searchPos = OrdinalPosition.Value - 1;

                // Skip back until we find a node with a one step shorter path that is our prefix
                while (searchPos >= 0 && !IsDirectChildPath(_elements[searchPos].Path, this.CurrentPath()))
                    searchPos--;

                if (searchPos == -1)
                    throw Error.InvalidOperation("Element list does not contain a parent for " + this.CurrentPath());

                OrdinalPosition = searchPos;
                return true;
            }
        }


        public virtual void Reset()
        {
            OrdinalPosition = null;
        }


//----------------------------------
//
// Methods that move to absolute locations
//
//----------------------------------

        public virtual Bookmark Bookmark()
        {
            return new Bookmark() { data = Current };
        }


        public virtual bool IsAtBookmark(Bookmark bookmark)
        {
            if (bookmark.data == null)
                 return OrdinalPosition == null;

            var elem = bookmark.data as Profile.ElementComponent;

            return this.Current == elem;
        }

        public virtual bool ReturnToBookmark(Bookmark bookmark)
        {
            if (bookmark.data == null)
            {
                OrdinalPosition = null;
                return true;
            }
            else
            {
                var elem = bookmark.data as Profile.ElementComponent;

                if (elem == null) return false;

                var index = _elements.IndexOf(elem);

                if (index != -1)
                {
                    OrdinalPosition = index;
                    return true;
                }
                else
                    return false;
            }
        }


   

//----------------------------------
//
// Methods that alter the list of elements
// 
//----------------------------------

        public bool InsertBefore(Profile.ElementComponent sibling)
        {
            if (!canInsertSiblingHere()) return false;

            var newSiblingPath = this.CurrentParentPath() + "." + sibling.GetNameFromPath();

            _elements.Insert(OrdinalPosition.Value, sibling);

            // Adjust the sibling's path so it's parent is the same as the current node
            sibling.Path = newSiblingPath;

            // Note: we're now positioned on the newly inserted element

            return true;
        }

        private bool canInsertSiblingHere()
        {
            // We're not positioned anywhere...
            if (OrdinalPosition == null) return false;

            // Cannot insert a sibling to the unique root element
            if (OrdinalPosition == 0) return false;

            return true;
        }

        public bool InsertAfter(Profile.ElementComponent sibling)
        {
            if (!canInsertSiblingHere()) return false;

            var insertPosition = positionAfter();
            var newSiblingPath = this.CurrentParentPath() + "." + sibling.GetNameFromPath();

            if (insertPosition == Count) // At last position
                _elements.Add(sibling);
            else
                _elements.Insert(insertPosition, sibling);

            // Adjust the sibling's path so it's parent is the same as the current node
            sibling.Path = newSiblingPath;

            // Navigate to newly inserted node
            OrdinalPosition = insertPosition;

            return true;
        }


        public bool InsertFirstChild(Profile.ElementComponent child)
        {
            if(Count == 0)
            {
                // Special case, insert a new root
                _elements.Add(child);
                child.Path = child.GetNameFromPath();
                OrdinalPosition = 0;
                return true;
            }
            else if (this.HasChildren())
                return false;       // Cannot insert another child, there's already one.
            else
            {
                var newSiblingPath = this.CurrentPath() + "." + child.GetNameFromPath();
                
                if (OrdinalPosition == Count - 1) // At last position
                    _elements.Add(child);
                else
                    _elements.Insert(OrdinalPosition.Value + 1, child);

                // Set the name to be a true child -> this actually creates a child
                child.Path = newSiblingPath;

                // Navigate to newly inserted child
                OrdinalPosition += 1;

                return true;
            }
        }

    
        public bool Delete()
        {
            if (OrdinalPosition == null) return false;

            // Can't delete a parent, need to delete children first
            if (this.HasChildren()) return false;

            // Special case, just the root is left
            if (_elements.Count == 1)
            {
                _elements.Clear();
                OrdinalPosition = null;
            }
            else if (MoveToNext())
            {
                // I have a next sibling, delete me and let my next sibling take my place as "current"
                MoveToPrevious();
                _elements.RemoveAt(OrdinalPosition.Value);
            }
            else if (MoveToPrevious())
            {
                // I am the last of the children, but I have preceding siblings, let the prevous take my place as "current"
                MoveToNext();
                _elements.RemoveAt(OrdinalPosition.Value);
                OrdinalPosition--;
            }
            else
            {
                // I am the last child of a parent, after I disappear, my parent will be "current"
                _elements.RemoveAt(OrdinalPosition.Value);
                OrdinalPosition--;
            }

            return true;
        }

        internal static bool IsDeeperPath(string me, string that)
        {
            return NumberOfParts(that) > NumberOfParts(me);
        }

        internal static bool IsDirectChildPath(string parent, string child)
        {
            return child.StartsWith(parent + ".") && child.IndexOf('.', parent.Length + 1) == -1;
        }

        internal static int NumberOfParts(string path)
        {
            var count = 1;
            for (var i = 0; i < path.Length; i++)
                if (path[i] == '.') count++;

            return count;
        }

    }

    public struct Bookmark
    {
        public object data;
    }
}
