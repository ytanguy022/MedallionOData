﻿using Medallion.OData.Client;
using Medallion.OData.Service.Sql;
using Medallion.OData.Tests.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Service.Sql
{
    public abstract class SqlTestBase
    {
        protected abstract ODataSqlContext Context { get; }

        [TestMethod]
        public void SqlSimpleSelect()
        {
            var names = this.Context.Query<Customer>("customers").Select(c => c.Name).ToArray();
            var dynamicNames = this.Context.Query<ODataEntity>("customers").Select(c => c.Get<string>("Name")).ToArray();
            using (var context = new CustomersContext())
            {
                var expectedNames = context.Customers.Select(c => c.Name).ToArray();
                names.CollectionShouldEqual(expectedNames);
                dynamicNames.CollectionShouldEqual(expectedNames);
            }
        }

        [TestMethod]
        public void SqlFilter()
        {
            var filtered = this.Context.Query<Customer>("customers")
                .Where(c => c.Name.Contains("b") || c.Salary > 100);
            var dynamicFiltered = this.Context.Query<ODataEntity>("customers")
                .Where(c => c.Get<string>("Name").Contains("b") || c.Get<double>("Salary") > 100);
            using (var context = new CustomersContext())
            {
                var expected = context.Customers.Where(c => c.Name.Contains("b") || c.Salary > 100);
                filtered.CollectionShouldEqual(expected);
                dynamicFiltered.Select(c => c.Get<Guid>("Id")).CollectionShouldEqual(expected.Select(c => c.Id));
            }
        }

        [TestMethod]
        public void SqlSort()
        {
            var sorted = this.Context.Query<Customer>("customers")
                .OrderBy(c => c.Salary)
                .ThenBy(c => c.Name.Length)
                .ThenByDescending(c => c.Name)
                .ToArray();
            var dynamicSorted = this.Context.Query<ODataEntity>("customers")
                .OrderBy(c => c.Get<double>("Salary"))
                .ThenBy(c => c.Get<string>("Name").Length)
                .ThenByDescending(c => c.Get<string>("Name"))
                .ToArray();
            using (var context = new CustomersContext())
            {
                var expected = context.Customers.OrderBy(c => c.Salary)
                    .ThenBy(c => c.Name.Length)
                    .ThenByDescending(c => c.Name)
                    .ToArray();
                sorted.CollectionShouldEqual(expected, orderMatters: true);
                dynamicSorted.Select(c => c.Get<string>("Name")).CollectionShouldEqual(expected.Select(c => c.Name), orderMatters: true);
            }
        }

        [TestMethod]
        public void SqlPaginate()
        {
            var sorted = this.Context.Query<Customer>("customers").OrderByDescending(c => c.Name.Length)
                .ThenBy(c => c.Name);
            var dynamicSorted = this.Context.Query<ODataEntity>("customers").OrderByDescending(c => c.Get<string>("Name").Length)
                .ThenBy(c => c.Get<string>("Name"))
                .Select(c => c.Get<Guid>("Id"));
            using (var context = new CustomersContext())
            {
                var expected = context.Customers.OrderByDescending(c => c.Name.Length)
                    .ThenBy(c => c.Name);
                sorted.Skip(3).Take(1).CollectionShouldEqual(expected.Skip(3).Take(1));
                dynamicSorted.Skip(1).Take(3).CollectionShouldEqual(expected.Select(c => c.Id).Skip(1).Take(3), orderMatters: true);
                sorted.Take(2).CollectionShouldEqual(expected.Take(2), orderMatters: true);
                dynamicSorted.Skip(2).CollectionShouldEqual(expected.Select(c => c.Id).Skip(2), orderMatters: true);
            }
        }

        [TestMethod]
        public void NullableEquality()
        {
            var samples = this.Context.Query<Sample>("samples");
            var dynamicSamples = this.Context.Query<ODataEntity>("samples");

            var nulls = samples.Where(s => s.NullableBool == null);
            var dynamicNulls = dynamicSamples.Where(s => s.Get<bool>("NullableBool") == null);
            var nonNulls = samples.Where(s => s.NullableBool != null);
            var dynamicNonNulls = dynamicSamples.Where(s => s.Get<bool?>("NullableBool") != null);
            var negatedNulls = samples.Where(s => !(s.NullableBool == null));
            var negatedDynamicNulls = dynamicSamples.Where(s => !(s.Get<bool?>("NullableBool") == null));
            var negatedNonNulls = samples.Where(s => !(s.NullableBool != null));
            var negatedDynamicNonNulls = dynamicSamples.Where(s => !(s.Get<bool?>("NullableBool") != null)); 

            using (var context = new CustomersContext())
            {
                var efNulls = context.Samples.Where(s => s.NullableBool == null).ToArray();
                nulls.CollectionShouldEqual(efNulls, "nulls");
                negatedNonNulls.CollectionShouldEqual(efNulls, "negatedNonNulls");
                dynamicNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNulls.Select(s => s.Id), "dynamicNulls");
                negatedDynamicNonNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNulls.Select(s => s.Id), "negatedDynamicNonNulls");
            }

            using (var context = new CustomersContext())
            {
                var efNonNulls = context.Samples.Where(s => s.NullableBool != null).ToArray();
                nonNulls.CollectionShouldEqual(efNonNulls, "nonNulls");
                negatedNulls.CollectionShouldEqual(efNonNulls, "negatedNulls");
                dynamicNonNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNonNulls.Select(s => s.Id), "dynamicNonNulls");
                negatedDynamicNulls.Select(s => s.Get<int>("Id")).CollectionShouldEqual(efNonNulls.Select(s => s.Id), "negatedDynamicNulls");
            }
        }
    }
}