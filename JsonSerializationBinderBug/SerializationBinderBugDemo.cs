using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace JsonSerializationBinderBug
{
    public class SerializationBinderBugDemo
    {
        [Fact]
        public void Demo()
        {
            using var stream = new MemoryStream();
            var binder = new KnownTypesBinder(typeof(TestClass<int, double, string>));
            var serializer = new JsonSerializer
            {
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = binder
            };
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                var container = new DataContainer
                {
                    Data = new TestClass<int, double, string> { Item1 = 42, Item2 = 3.1415, Item3 = "Luke" }
                };
                serializer.Serialize(jsonWriter, container);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var jsonString = reader.ReadToEnd();

            binder.BindToName(typeof(TestClass<int, double, string>), out var assemblyName, out var typeName);
            Assert.Equal("TestClass`3[Int32,Double,String]", typeName);

            var jObject = JObject.Parse(jsonString);
            typeName = (string)jObject["Data"]["$type"];
            
            Assert.Equal("TestClass`3[Int32,Double,String]", typeName);

            // ------------ BUG -------------
            // Assert.Equal() Failure
            //                                   ↓ (pos 24)
            // Expected: ···Class`3[Int32, Double, String]
            // Actual:   ···Class`3[Int32, Double]
            //                                   ↑ (pos 24)

            // ------------ WORKAROUND -------------
            // If KnownTypesBinder uses other char than "[" and "," then this works correct.
        }
    }

    internal class DataContainer { public object? Data { get; set; } }

    internal class TestClass<U, V, W>
    {
        public U Item1 { get; set; }
        public V Item2 { get; set; }
        public W Item3 { get; set; }
    }

    internal sealed class KnownTypesBinder : ISerializationBinder
    {
        private readonly Dictionary<string, Type> typeNameToType;
        private readonly Dictionary<Type, string> typeToTypeName;

        public KnownTypesBinder(params Type[] knownTypes)
        {
            typeNameToType = knownTypes.ToDictionary(GetTypeName, x => x);
            typeToTypeName = knownTypes.ToDictionary(x => x, GetTypeName);
        }

        public Type BindToType(string? assemblyName, string typeName) => typeNameToType.TryGetValue(typeName, out var type) ? type : null!;
        
        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            if (!typeToTypeName.TryGetValue(serializedType, out typeName)) throw new SerializationException("Type is not registered as known type: " + serializedType);
            assemblyName = null;
        }

        private static string GetTypeName(Type type)
        {
            if (!type.IsGenericType) return type.Name;
            return type.Name + "[" + string.Join(",", type.GenericTypeArguments.Select(GetTypeName)) + "]";
        }
    }
}
