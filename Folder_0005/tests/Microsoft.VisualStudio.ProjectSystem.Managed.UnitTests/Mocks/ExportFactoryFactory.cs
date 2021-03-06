// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace System.ComponentModel.Composition
{
    internal static class ExportFactoryFactory
    {
        public static ExportFactory<T> ImplementCreateValueWithAutoDispose<T>(Func<T> factory)
        {
            return new ExportFactory<T>(() =>
            {
                T value = factory();

                return Tuple.Create<T, Action>(value, () =>
                {
                    if (value is IDisposable disposable)
                        disposable.Dispose();
                });

            });
        }

        public static ExportFactory<T, TMetadata> ImplementCreateValueWithAutoDispose<T, TMetadata>(Func<T> factory, TMetadata metadata)
        {
            return new ExportFactory<T, TMetadata>(() =>
            {
                T value = factory();

                return Tuple.Create<T, Action>(value, () =>
                {
                    if (value is IDisposable disposable)
                        disposable.Dispose();
                });

            }, metadata);
        }
    }
}
