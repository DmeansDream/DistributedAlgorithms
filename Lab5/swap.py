import multiprocessing as mp
import time
import hashlib

def hash_secret(secret):
    return hashlib.sha256(secret.encode()).hexdigest()

def exec_contract(contract, secret):
    if time.time() > contract['time_lock']:
        #contract['state'] = 'refunded'
        return False, "Contract refunded"

    if hash_secret(secret) == contract['hash_lock']:
        contract['state'] = 'claimed'
        return True, "Contract claimed"

    return False, "Invalid Secret"

def refund(name, accounts, registry, lock, out_id):
    contract = registry[out_id]

    if contract['state'] == 'locked' and time.time() > contract['time_lock']:
        curr = contract['currency']
        amount = contract['amount']
        with lock:
            wallet = accounts[name]
            wallet[curr] += amount
            accounts[name] = wallet

        contract['state'] = 'refunded'
        print(f"{name}: Swap failed, refunded {curr}:{amount}")
    elif contract['state'] == 'locked':
        print(f"{name}: Cannot refund, time has not come")

def client(name, is_initiator, accounts, registry, lock, timeout,
           channel_in, channel_out, out_id, in_id, curr, amount ):

    print(f"{name} is starting")

    if is_initiator:
        secret = "passwd"
        hashed = hash_secret(secret)
        print(f"{name} has hashed \"{secret}\" and got: {hashed}")
    else:
        print(f"{name} is waiting for contracts in registry...")
        while registry[in_id].get('state') != 'locked':
            time.sleep(0.5)

        hashed = registry[in_id]['hash_lock']
        print(f"{name} has got \"{in_id}\" lock")

    with lock:
        wallet = accounts[name]
        wallet[curr] -= amount
        accounts[name] = wallet

    out_contract = registry[out_id]
    out_contract['hash_lock'] = hashed
    out_contract['time_lock'] = time.time() + timeout
    out_contract['amount'] = amount
    out_contract['currency'] = curr
    out_contract['state'] = 'locked'

    print(f"{name} locked {curr}:{amount} in registry : {out_id}")

    in_contract = registry[in_id]

    if is_initiator:
        print(f"{name} initiator is awaiting final contract to lock...")

        while in_contract.get('state') != 'locked':
            time.sleep(0.5)

        try:
            # ////INTRODUCE ERROR HERE\\\\
            secret = 'falseSecret'
            # \\\\                    ////
            print(f"{name} is initiating exchange!")
            status, msg = exec_contract(in_contract, secret)

            if status:
                with lock:
                    wallet = accounts[name]
                    wallet[in_contract['currency']] += in_contract['amount']
                    accounts[name] = wallet
                print(f"{name}: exchange success: {msg}, received {in_contract['amount']} of {in_contract['currency']}")

                if channel_out:
                    channel_out.put(secret)
            else:
                print(f"{name}: exchange FAIL: {msg}")
        except Exception:
            print(f"{name}: exchange ERROR")
        finally:
            if registry[out_id]['state'] == 'locked':
                remaining_time = registry[out_id].get('time_lock') - time.time()
                if remaining_time > 0:
                    print(f"{name}: waits until timeout occurs...")
                    time.sleep(remaining_time + 0.1)

                refund(name, accounts, registry, lock, out_id)

    else:
        print(f"{name}: waiting for secret to be passed through channel...")
        try:
            passed_secret = channel_in.get(timeout=10)
            status, msg = exec_contract(in_contract, passed_secret)

            if status:
                with lock:
                    wallet = accounts[name]
                    wallet[in_contract['currency']] += in_contract['amount']
                    accounts[name] = wallet
                print(f"{name}: exchange success: {msg}, received {in_contract['amount']} of {in_contract['currency']}")

                if channel_out:
                    channel_out.put(passed_secret)
            else:
                print(f"{name}: exchange FAIL: {msg}")
        except Exception:
            print(f"{name}: exchange ERROR")
        finally:
            if registry[out_id]['state'] == 'locked':
                remaining_time = registry[out_id].get('time_lock') - time.time()
                if remaining_time > 0:
                    print(f"{name}: waits until timeout occurs...")
                    time.sleep(remaining_time + 0.1)

                refund(name, accounts, registry, lock, out_id)

def arbiter():
    timeout = 5.0
    with mp.Manager() as manager:
        registry = manager.dict({
            'AB' : manager.dict({'state' : 'pending'}),
            'BC' : manager.dict({'state' : 'pending'}),
            'CA' : manager.dict({'state' : 'pending'})
        })

        accounts = manager.dict({
            'A' : {'BTC': 10, 'ETH': 0,  'COIN': 0},
            'B' : {'BTC': 0, 'ETH': 0,  'COIN': 1000},
            'C' : {'BTC': 0, 'ETH': 50,  'COIN': 0}
        })

        account_lock = manager.Lock()

        channel_AC = manager.Queue()
        channel_CB = manager.Queue()

        print("Starting balances")
        for user, bal in accounts.items():
            print(f"{user}: {bal}")

        #    B -COIN:1000-> <-BTC:10- A -COIN:1000> <-ETH:50- C

        A_p = mp.Process(target=client,
                         args=('A', True, accounts, registry, account_lock, timeout * 3,
                               None, channel_AC, 'AB', 'CA', 'BTC', 10))
        B_p = mp.Process(target=client,
                         args=('B', False, accounts, registry, account_lock, timeout * 2,
                               channel_CB, None, 'BC', 'AB', 'COIN', 1000))
        C_p = mp.Process(target=client,
                         args=('C', False, accounts, registry, account_lock, timeout,
                               channel_AC, channel_CB, 'CA', 'BC', 'ETH', 50))

        A_p.start()
        B_p.start()
        C_p.start()

        A_p.join()
        B_p.join()
        C_p.join()

        for id, data in registry.items():
            print(f"Contract {id} : {data}")

        print("Final Account balances")
        for id, data in accounts.items():
            print(f"Person {id} : {data}")

if __name__ == '__main__':
    arbiter()

