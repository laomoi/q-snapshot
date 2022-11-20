local qsnapshot = {}

rawset(_G, 'qsnapshot', qsnapshot)

local weak_meta_table = {__mode = 'k', __ignore_qsnapshot = true}
setmetatable(qsnapshot, weak_meta_table)


-- qsnapshot.test = function()
--     local a ={}
--     setmetatable(a, weak_meta_table)
--     return getmetatable(a)
-- end


qsnapshot.get_metatable = function(t)
    return getmetatable(t)
end

qsnapshot.get_registry = function()
    return debug.getregistry()
end

qsnapshot.get_func_source = function(func)
    local info = debug.getinfo(func, 'Sl')
    local result = string.format('func:%s&line:%d', info.source, info.linedefined)
    return result
end
   
qsnapshot.get_memory_usage = function()
    return math.floor(collectgarbage("count"))
end
return qsnapshot
