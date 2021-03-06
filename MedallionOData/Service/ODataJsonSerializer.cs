﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData.Trees;
using Newtonsoft.Json;

using PropertyPath = System.Collections.Generic.IReadOnlyList<System.Reflection.PropertyInfo>;
using System.IO;
using System.Collections;
using Medallion.OData.Client;

namespace Medallion.OData.Service
{
    /// <summary>
    /// Serializes to OData's JSON lite format
    /// </summary>
	public sealed class ODataJsonSerializer : IODataSerializer
	{
        // MA: verified that this is safe: http://json.codeplex.com/discussions/110461
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        object IODataSerializer.Serialize<TElement>(IODataProjectResult<TElement> projectResult)
        {
            Throw.IfNull(projectResult, "projectResult");
 
            // TODO VNext this doesn't seem like the best place for this logic. Consider changing
            // the pipeline APIs so that this logic can happen earlier
            IEnumerable projectedQuery;
            int? inlineCount;
            PaginationHelper.Paginate(projectResult, out projectedQuery, out inlineCount);

            var result = this.Serialize(projectedQuery, projectResult.ProjectMapping, inlineCount);
            return result;
        }

        // TODO VNext consider returning object here, allowing us to use JTokenWriter at times instead of JsonTextWriter
        internal string Serialize(IEnumerable projectedQuery, IReadOnlyDictionary<ODataSelectColumnExpression, PropertyPath> projectMapping, int? inlineCount)
        {
            Throw.IfNull(projectedQuery, "projectedQuery");
            Throw.IfNull(projectMapping, "projectMapping");

            var node = Node.Create(projectMapping.Select(kvp => KeyValuePair.Create(kvp.Key, new ValueRetriever(kvp.Value))));

            using (var stringWriter = new StringWriter())
            {
                using (var writer = new JsonTextWriter(stringWriter) { CloseOutput = false })
                {
                    writer.WriteStartObject();

                    if (inlineCount.HasValue) 
                    {
                        writer.WritePropertyName("odata.count");
                        writer.WriteValue(inlineCount.Value);
                    }

                    writer.WritePropertyName("value");
                    writer.WriteStartArray();
                    foreach (var item in projectedQuery)
                    {
                        WriteNode(item, node, writer);
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                }
                var result = stringWriter.ToString();
                return result;
            }
        }

        private static void WriteNode(object item, Node node, JsonWriter writer)
        {
            var oDataType = node.Property == null ? ODataExpressionType.Complex : node.Property.PropertyType.ToODataExpressionType();
            object value;
            if (oDataType == ODataExpressionType.Complex)
            {
                IEnumerable<KeyValuePair<string, object>> simpleProperties;
                if (node.Select != null)
                {
                    Throw<InvalidOperationException>.If(!node.Select.AllColumns, "should have all columns!");
                    if (!node.ValueRetriever.TryGetValue(item, out value) || value == null)
                    {
                        writer.WriteNull();
                        return;
                    }

                    var oDataEntity = value as ODataEntity;
                    if (oDataEntity != null)
                    {
                        simpleProperties = oDataEntity.Values.Where(kvp => kvp.Value == null || kvp.Value.GetType().ToODataExpressionType() != ODataExpressionType.Complex);
                    }
                    else
                    {
                        simpleProperties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(pi => pi.CanRead && pi.PropertyType.ToODataExpressionType() != ODataExpressionType.Complex)
                            .Select(pi => KeyValuePair.Create(pi.Name, pi.GetValue(value)));
                    }
                }
                else
                {
                    simpleProperties = Empty<KeyValuePair<string, object>>.Array;
                    value = null;
                }

                writer.WriteStartObject();
                foreach (var kvp in simpleProperties)
                {
                    writer.WritePropertyName(kvp.Key);
                    Serializer.Serialize(writer, kvp.Value);
                }
                foreach (var child in node.Children)
                {
                    writer.WritePropertyName(child.Property.Name);
                    WriteNode(item, child, writer);
                }
                writer.WriteEndObject();
            }
            else if (node.ValueRetriever.TryGetValue(item, out value))
            {
                Serializer.Serialize(writer, value);
            }
        }

        private class Node
        {
            public PropertyInfo Property { get; private set; }
            public ODataSelectColumnExpression Select { get; private set; }
            public ValueRetriever ValueRetriever { get; private set; }
            public IEnumerable<Node> Children { get { return this._childrenByProperty.Values; } }
            private readonly Dictionary<PropertyInfo, Node> _childrenByProperty = new Dictionary<PropertyInfo, Node>(Helpers.MemberComparer);

            public static Node Create(IEnumerable<KeyValuePair<ODataSelectColumnExpression, ValueRetriever>> selects) 
            {
                var root = new Node();
                foreach (var kvp in selects) 
                {
                    Augment(root, kvp.Key, kvp.Value);
                }
                return root;
            }

            private static void Augment(Node root, ODataSelectColumnExpression select, ValueRetriever valueRetriever) 
            {
                var node = GetOrCreateMemberNode(root, select.Expression);
                if (node.Select == null) 
                {
                    node.Select = select;
                    node.ValueRetriever = valueRetriever;
                }
            }

            private static Node GetOrCreateMemberNode(Node root, ODataMemberAccessExpression memberExpression) 
            {
                if (memberExpression == null) 
                {
                    return root;
                }

                var parent = GetOrCreateMemberNode(root, memberExpression.Expression);

                Node existing;
                if (parent._childrenByProperty.TryGetValue(memberExpression.Member, out existing)) 
                {
                    return existing;
                }

                var newNode = new Node { Property = memberExpression.Member };
                parent._childrenByProperty.Add(newNode.Property, newNode);
                return newNode;
            }
        }

        private class ValueRetriever
        {
            private readonly PropertyPath _path;

            public ValueRetriever(PropertyPath path)
            {
                this._path = path;
            }

            public bool TryGetValue(object item, out object value)
            {
                var result = item;
                foreach (var prop in this._path)
                {
                    if (result == null)
                    {
                        value = null;
                        return false;
                    }
                    else
                    {
                        result = prop.GetValue(result);
                    }
                }

                value = result;
                return true;
            }
        }
    }
}
