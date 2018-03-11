using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans;
using Orleans.Runtime;

namespace OrleansSaga.Grains.Queue
{
    public class CommandGrain : Grain, ICommandGrain
    {
        protected Logger Logger { get; set; }

        public override Task OnActivateAsync()
        {
            var key = this.GetPrimaryKeyLong();
            Logger = GetLogger($"CommandGrain-{key}");
            return base.OnActivateAsync();
        }

        public async Task Execute()
        {
            var key = this.GetPrimaryKeyLong();
            Logger.Info($"Execute Command {key}");

            //var key = Expression.Parameter(typeof(long), "primaryKey");
            //var mi = typeof(IGrainFactory).GetMethod("GetGrain", new Type[] { typeof(long), typeof(string) });
            //var gmi = mi.MakeGenericMethod(new Type[] { typeof(ICommandGrain) });
            //var command = (ICommandGrain)gmi.Invoke(GrainFactory, new object[] { key, null });
            //var call = Expression.Call(, key);
            //var exp = Expression.Lambda<Func<long, ICommandGrain>>(call, key);

            //var f = exp.Compile();
            //var command = f(commandId);
            Expression<Func<ICommandGrain, Task>> e = c => c.Log("Expression Execute Command");


            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                //TypeNameHandling = TypeNameHandling.All,
                //PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                //DateFormatHandling = DateFormatHandling.IsoDateFormat,
                //DefaultValueHandling = DefaultValueHandling.Ignore,
                //MissingMemberHandling = MissingMemberHandling.Ignore,
                //NullValueHandling = NullValueHandling.Ignore,
                //ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            };
            serializerSettings.Converters.Add(new TypeJsonConverter());
            serializerSettings.Converters.Add(new MethodInfoJsonConverter());
            serializerSettings.Converters.Add(new ExpressionJsonConverter());

            var s = JsonConvert.SerializeObject(e, serializerSettings);
            var e2 = JsonConvert.DeserializeObject<LambdaExpression>(s, serializerSettings);
            var s2 = JsonConvert.SerializeObject(e2, serializerSettings);
            var f = e2.Compile();
            var t = (Task)f.DynamicInvoke(this);
            await t;
            //await f(this);

            //var delay = new Random().Next(100, 500);
            //await Task.Delay(delay);
        }

        public Task Log(string s)
        {
            Logger.Info($"Log {s}");
            return Task.CompletedTask;
        }
    }

    public interface ICommandGrain : IGrainWithIntegerKey
    {
        Task Execute();

        Task Log(string s);
    }

    public class TypeJsonConverter : JsonConverter
    {
        private static readonly Type TypeOfType = typeof(Type);
        public override bool CanConvert(Type objectType) => objectType == TypeOfType || objectType.IsSubclassOf(TypeOfType);
        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            return Type.GetType(token.Value<string>());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public class MethodInfoJsonConverter : JsonConverter
    {
        private static readonly Type TypeOfMethodInfo = typeof(MethodInfo);

        public override bool CanConvert(Type objectType) => objectType == TypeOfMethodInfo || objectType.IsSubclassOf(TypeOfMethodInfo);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            return Deserialize(token, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public MethodInfo Deserialize(JToken token, JsonSerializer serializer)
        {
            var name = token["Name"].Value<string>();
            var signature = token["Signature"].Value<string>();
            var assemblyName = token["AssemblyName"].Value<string>();
            var className = token["ClassName"].Value<string>();
            var genericArguments = token["GenericArguments"].ToObject<Type[]>(serializer);

            var type = Type.GetType(className);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var method = methods.First(m => m.Name == name && m.ToString() == signature);

            if (genericArguments != null && method.IsGenericMethodDefinition)
            {
                return method.MakeGenericMethod(genericArguments);
            }
            return method;
        }
    }

    public class ExpressionJsonConverter : JsonConverter
    {
        private static readonly Type TypeOfExpression = typeof(Expression);

        private readonly Dictionary<string, ParameterExpression> _parameterExpressions = new Dictionary<string, ParameterExpression>();

        public override bool CanConvert(Type objectType) => objectType == TypeOfExpression || objectType.IsSubclassOf(TypeOfExpression);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            return Deserialize<Expression>(token, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(JToken token, JsonSerializer serializer) where T : Expression
        {
            var nodeType = token["NodeType"].ToObject<ExpressionType>(serializer);
            switch (nodeType)
            {
                case ExpressionType.Lambda:
                    return Expression.Lambda(Deserialize<Expression>(token["Body"], serializer), token["Parameters"].ToObject<ParameterExpression[]>(serializer)) as T;
                case ExpressionType.Call:
                    return Expression.Call(Deserialize<Expression>(token["Object"], serializer), token["Method"].ToObject<MethodInfo>(serializer), token["Arguments"].ToObject<Expression[]>(serializer)) as T;
                case ExpressionType.Parameter:
                    return Parameter(token, serializer) as T;
                case ExpressionType.Constant:
                    return Constant(token, serializer) as T;
                default:
                    throw new NotImplementedException();
            }
        }

        private ParameterExpression Parameter(JToken token, JsonSerializer serializer)
        {
            var name = token["Name"].Value<string>();
            if (_parameterExpressions.TryGetValue(name, out ParameterExpression value))
            {
                return value;
            }
            var type = Type.GetType(token["Type"].Value<string>());
            value = Expression.Parameter(type, name);
            _parameterExpressions.Add(name, value);
            return value;
        }

        private ConstantExpression Constant(JToken token, JsonSerializer serializer)
        {
            var type = Type.GetType(token["Type"].Value<string>());
            var value = token["Value"].ToObject(type, serializer);
            return Expression.Constant(value, type);
        }
    }
}
