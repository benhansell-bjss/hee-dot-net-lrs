﻿using Doctrina.ExperienceApi.Data.Exceptions;
using Doctrina.ExperienceApi.Data.InteractionTypes;
using Doctrina.ExperienceApi.Data.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Doctrina.ExperienceApi.Data
{
    public class Activity : StatementObjectBase, IStatementObject, IActivity
    {
        public Activity() { }

        public Activity(JsonString jsonString) : this(jsonString.ToJToken(), ApiVersion.GetLatest()) { }

        public Activity(JToken jobj, ApiVersion version) : base(jobj, version)
        {
            GuardType(jobj, JTokenType.Object);
            var id = jobj["id"];
            if (id != null)
            {
                GuardType(id, JTokenType.String);
                Id = new Iri(jobj.Value<string>("id"));
            }

            var definition = jobj["definition"];
            if (definition != null)
            {
                GuardType(definition, JTokenType.Object);
                JToken interactionType = definition["interactionType"];
                if (interactionType != null)
                {
                    GuardType(interactionType, JTokenType.String);
                    try
                    {
                        InteractionType type = interactionType.Value<string>();
                        Definition = type.CreateInstance(definition, version);
                    }
                    catch (InteractionTypeException ex)
                    {
                        throw new JsonTokenModelException(interactionType, ex.Message);
                    }
                }
                else
                {
                    GuardAdditionalProperties((JObject)definition,
                        "type", "name", "description", "moreInfo", "extensions"
                    );
                    Definition = new ActivityDefinition(definition, version);
                }
            }
        }

        protected override ObjectType OBJECT_TYPE => ObjectType.Activity;

        /// <summary>
        /// 
        /// </summary>
        public Iri Id { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ActivityDefinition Definition { get; set; }

        public override JToken ToJToken(ApiVersion version, ResultFormat format)
        {
            var result = base.ToJToken(version, format);

            if (Id != null)
            {
                result["id"] = Id.ToString();
            }

            if (Definition != null)
            {
                result["definition"] = Definition.ToJToken(version, format);
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            var activity = obj as Activity;
            return activity != null &&
                   base.Equals(obj) &&
                   EqualityComparer<Iri>.Default.Equals(Id, activity.Id);
        }

        public override int GetHashCode()
        {
            return 2108858624 + EqualityComparer<Iri>.Default.GetHashCode(Id);
        }

        public static bool operator ==(Activity activity1, Activity activity2)
        {
            return EqualityComparer<Activity>.Default.Equals(activity1, activity2);
        }

        public static bool operator !=(Activity activity1, Activity activity2)
        {
            return !(activity1 == activity2);
        }
    }
}
