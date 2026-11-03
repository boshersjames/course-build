import json, os, sys, time, glob
P=sys.argv[1]; inbox=P+'/Automation/inbox'; outbox=P+'/Automation/outbox'; os.makedirs(inbox,exist_ok=True); os.makedirs(outbox,exist_ok=True)
t_end=time.time()+90
while time.time()<t_end:
    for f in sorted(glob.glob(inbox+'/*.json')):
        c=json.load(open(f)); os.remove(f); a={'id':c['id'],'cmd':c['cmd'],'status':'ok','message':''}
        if c['cmd']=='ping': a['message']='Unity 2018.2.8f1 (fake)'
        elif c['cmd']=='describe' and c['tool']=='SpeedTreeOSMImporter': a['status']='error'; a['message']="tool SpeedTreeOSMImporter isn't compiled in this project"
        elif c['cmd']=='invoke':
            s={x['field']:x['value'] for x in c.get('set',[])}; a['message']='%s.%s done with %s'%(c['tool'],c['method'],s)
            if c['tool']=='RenderQueue4K': a['message']+=' (fake: no frames)'
        json.dump(a,open(outbox+'/'+c['id']+'.json','w'))
    time.sleep(0.3)
