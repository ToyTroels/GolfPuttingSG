using System.Text.Json.Serialization;
using GolfSG.Core.Models;

namespace GolfSG.Application.Rounds.Sync;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Round))]
[JsonSerializable(typeof(List<RemoteRound>))]
[JsonSerializable(typeof(RemoteRound))]
[JsonSerializable(typeof(RoundWriteRequest))]
public partial class RoundSyncJsonContext : JsonSerializerContext;
