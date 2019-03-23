using System;
using System.Threading.Tasks;

namespace ProcessGuardian {
	class Program {
		static async Task Main(string[] args) {
			Monitor mon = new Monitor();
			await mon.Watch();
		}
	}
}



