using ZLinq;

// ZLinq drop-in everything
[assembly: ZLinqDropInAttribute("", ZLinq.DropInGenerateTypes.Everything)]
[assembly: ZLinqDropInExternalExtension("", "System.Collections.Immutable.ImmutableArray`1", "ZLinq.Linq.FromImmutableArray`1")]
