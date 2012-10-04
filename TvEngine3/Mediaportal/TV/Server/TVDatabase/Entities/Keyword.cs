//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.Serialization;

namespace Mediaportal.TV.Server.TVDatabase.Entities
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(PersonalTVGuideMap))]
    [KnownType(typeof(KeywordMap))]
    public partial class Keyword: IObjectWithChangeTracker, INotifyPropertyChanged
    {
        #region Primitive Properties
    
        [DataMember]
        public int IdKeyword
        {
            get { return _idKeyword; }
            set
            {
                if (_idKeyword != value)
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added)
                    {
                        throw new InvalidOperationException("The property 'IdKeyword' is part of the object's key and cannot be changed. Changes to key properties can only be made when the object is not being tracked or is in the Added state.");
                    }
                    _idKeyword = value;
                    OnPropertyChanged("IdKeyword");
                }
            }
        }
        private int _idKeyword;
    
        [DataMember]
        public string KeywordName
        {
            get { return _keywordName; }
            set
            {
                if (_keywordName != value)
                {
                    _keywordName = value;
                    OnPropertyChanged("KeywordName");
                }
            }
        }
        private string _keywordName;
    
        [DataMember]
        public int Rating
        {
            get { return _rating; }
            set
            {
                if (_rating != value)
                {
                    _rating = value;
                    OnPropertyChanged("Rating");
                }
            }
        }
        private int _rating;
    
        [DataMember]
        public bool AutoRecord
        {
            get { return _autoRecord; }
            set
            {
                if (_autoRecord != value)
                {
                    _autoRecord = value;
                    OnPropertyChanged("AutoRecord");
                }
            }
        }
        private bool _autoRecord;
    
        [DataMember]
        public int SearchIn
        {
            get { return _searchIn; }
            set
            {
                if (_searchIn != value)
                {
                    _searchIn = value;
                    OnPropertyChanged("SearchIn");
                }
            }
        }
        private int _searchIn;

        #endregion
        #region Navigation Properties
    
        [DataMember]
        public TrackableCollection<PersonalTVGuideMap> PersonalTVGuideMaps
        {
            get
            {
                if (_personalTVGuideMaps == null)
                {
                    _personalTVGuideMaps = new TrackableCollection<PersonalTVGuideMap>();
                    _personalTVGuideMaps.CollectionChanged += FixupPersonalTVGuideMaps;
                }
                return _personalTVGuideMaps;
            }
            set
            {
                if (!ReferenceEquals(_personalTVGuideMaps, value))
                {
                    if (ChangeTracker.ChangeTrackingEnabled)
                    {
                        throw new InvalidOperationException("Cannot set the FixupChangeTrackingCollection when ChangeTracking is enabled");
                    }
                    if (_personalTVGuideMaps != null)
                    {
                        _personalTVGuideMaps.CollectionChanged -= FixupPersonalTVGuideMaps;
                        // This is the principal end in an association that performs cascade deletes.
                        // Remove the cascade delete event handler for any entities in the current collection.
                        foreach (PersonalTVGuideMap item in _personalTVGuideMaps)
                        {
                            ChangeTracker.ObjectStateChanging -= item.HandleCascadeDelete;
                        }
                    }
                    _personalTVGuideMaps = value;
                    if (_personalTVGuideMaps != null)
                    {
                        _personalTVGuideMaps.CollectionChanged += FixupPersonalTVGuideMaps;
                        // This is the principal end in an association that performs cascade deletes.
                        // Add the cascade delete event handler for any entities that are already in the new collection.
                        foreach (PersonalTVGuideMap item in _personalTVGuideMaps)
                        {
                            ChangeTracker.ObjectStateChanging += item.HandleCascadeDelete;
                        }
                    }
                    OnNavigationPropertyChanged("PersonalTVGuideMaps");
                }
            }
        }
        private TrackableCollection<PersonalTVGuideMap> _personalTVGuideMaps;
    
        [DataMember]
        public TrackableCollection<KeywordMap> KeywordMaps
        {
            get
            {
                if (_keywordMaps == null)
                {
                    _keywordMaps = new TrackableCollection<KeywordMap>();
                    _keywordMaps.CollectionChanged += FixupKeywordMaps;
                }
                return _keywordMaps;
            }
            set
            {
                if (!ReferenceEquals(_keywordMaps, value))
                {
                    if (ChangeTracker.ChangeTrackingEnabled)
                    {
                        throw new InvalidOperationException("Cannot set the FixupChangeTrackingCollection when ChangeTracking is enabled");
                    }
                    if (_keywordMaps != null)
                    {
                        _keywordMaps.CollectionChanged -= FixupKeywordMaps;
                        // This is the principal end in an association that performs cascade deletes.
                        // Remove the cascade delete event handler for any entities in the current collection.
                        foreach (KeywordMap item in _keywordMaps)
                        {
                            ChangeTracker.ObjectStateChanging -= item.HandleCascadeDelete;
                        }
                    }
                    _keywordMaps = value;
                    if (_keywordMaps != null)
                    {
                        _keywordMaps.CollectionChanged += FixupKeywordMaps;
                        // This is the principal end in an association that performs cascade deletes.
                        // Add the cascade delete event handler for any entities that are already in the new collection.
                        foreach (KeywordMap item in _keywordMaps)
                        {
                            ChangeTracker.ObjectStateChanging += item.HandleCascadeDelete;
                        }
                    }
                    OnNavigationPropertyChanged("KeywordMaps");
                }
            }
        }
        private TrackableCollection<KeywordMap> _keywordMaps;

        #endregion
        #region ChangeTracking
    
        protected virtual void OnPropertyChanged(String propertyName)
        {
            if (ChangeTracker.State != ObjectState.Added && ChangeTracker.State != ObjectState.Deleted)
            {
                ChangeTracker.State = ObjectState.Modified;
            }
            if (_propertyChanged != null)
            {
                _propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    
        protected virtual void OnNavigationPropertyChanged(String propertyName)
        {
            if (_propertyChanged != null)
            {
                _propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged{ add { _propertyChanged += value; } remove { _propertyChanged -= value; } }
        private event PropertyChangedEventHandler _propertyChanged;
        private ObjectChangeTracker _changeTracker;
    
        [DataMember]
        public ObjectChangeTracker ChangeTracker
        {
            get
            {
                if (_changeTracker == null)
                {
                    _changeTracker = new ObjectChangeTracker();
                    _changeTracker.ObjectStateChanging += HandleObjectStateChanging;
                }
                return _changeTracker;
            }
            set
            {
                if(_changeTracker != null)
                {
                    _changeTracker.ObjectStateChanging -= HandleObjectStateChanging;
                }
                _changeTracker = value;
                if(_changeTracker != null)
                {
                    _changeTracker.ObjectStateChanging += HandleObjectStateChanging;
                }
            }
        }
    
        private void HandleObjectStateChanging(object sender, ObjectStateChangingEventArgs e)
        {
            if (e.NewState == ObjectState.Deleted)
            {
                ClearNavigationProperties();
            }
        }
    
        protected bool IsDeserializing { get; private set; }
    
        [OnDeserializing]
        public void OnDeserializingMethod(StreamingContext context)
        {
            IsDeserializing = true;
        }
    
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context)
        {
            IsDeserializing = false;
            ChangeTracker.ChangeTrackingEnabled = true;
        }
    
        protected virtual void ClearNavigationProperties()
        {
            PersonalTVGuideMaps.Clear();
            KeywordMaps.Clear();
        }

        #endregion
        #region Association Fixup
    
        private void FixupPersonalTVGuideMaps(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (e.NewItems != null)
            {
                foreach (PersonalTVGuideMap item in e.NewItems)
                {
                    item.Keyword = this;
                    if (ChangeTracker.ChangeTrackingEnabled)
                    {
                        if (!item.ChangeTracker.ChangeTrackingEnabled)
                        {
                            item.StartTracking();
                        }
                        ChangeTracker.RecordAdditionToCollectionProperties("PersonalTVGuideMaps", item);
                    }
                    // This is the principal end in an association that performs cascade deletes.
                    // Update the event listener to refer to the new dependent.
                    ChangeTracker.ObjectStateChanging += item.HandleCascadeDelete;
                }
            }
    
            if (e.OldItems != null)
            {
                foreach (PersonalTVGuideMap item in e.OldItems)
                {
                    if (ReferenceEquals(item.Keyword, this))
                    {
                        item.Keyword = null;
                    }
                    if (ChangeTracker.ChangeTrackingEnabled)
                    {
                        ChangeTracker.RecordRemovalFromCollectionProperties("PersonalTVGuideMaps", item);
                    }
                    // This is the principal end in an association that performs cascade deletes.
                    // Remove the previous dependent from the event listener.
                    ChangeTracker.ObjectStateChanging -= item.HandleCascadeDelete;
                }
            }
        }
    
        private void FixupKeywordMaps(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (e.NewItems != null)
            {
                foreach (KeywordMap item in e.NewItems)
                {
                    item.Keyword = this;
                    if (ChangeTracker.ChangeTrackingEnabled)
                    {
                        if (!item.ChangeTracker.ChangeTrackingEnabled)
                        {
                            item.StartTracking();
                        }
                        ChangeTracker.RecordAdditionToCollectionProperties("KeywordMaps", item);
                    }
                    // This is the principal end in an association that performs cascade deletes.
                    // Update the event listener to refer to the new dependent.
                    ChangeTracker.ObjectStateChanging += item.HandleCascadeDelete;
                }
            }
    
            if (e.OldItems != null)
            {
                foreach (KeywordMap item in e.OldItems)
                {
                    if (ReferenceEquals(item.Keyword, this))
                    {
                        item.Keyword = null;
                    }
                    if (ChangeTracker.ChangeTrackingEnabled)
                    {
                        ChangeTracker.RecordRemovalFromCollectionProperties("KeywordMaps", item);
                    }
                    // This is the principal end in an association that performs cascade deletes.
                    // Remove the previous dependent from the event listener.
                    ChangeTracker.ObjectStateChanging -= item.HandleCascadeDelete;
                }
            }
        }

        #endregion
    }
}
